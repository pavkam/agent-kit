// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

/// <summary>
/// The default <see cref="IToolInvoker"/>: resolves a call's requested
/// <see cref="ToolReference"/> against the registered <see cref="IToolCatalog"/>,
/// authorizes it through the registered <see cref="IToolAuthorizer"/>, and
/// invokes the resolved tool, translating every reachable failure into an
/// ordinary <see cref="ResolvedToolInvocation"/>.
/// </summary>
/// <remarks>
/// This class never lets a tool's thrown exception propagate as a fault to
/// its caller: an unknown tool, a denied authorization, and an
/// unhandled exception from <see cref="ITool.InvokeAsync"/> all become a
/// <see cref="ToolInvocationResult"/> whose <see cref="ToolCallOutcome.Kind"/>
/// is <see cref="ToolCallOutcomeKind.Rejected"/> or
/// <see cref="ToolCallOutcomeKind.Failed"/> respectively. Cancellation is
/// the one exception allowed to propagate when the caller's token is actually
/// canceled, since that represents the caller itself abandoning the operation
/// rather than a terminal outcome for it. An implementation-thrown
/// <see cref="OperationCanceledException"/> without caller cancellation is an
/// ordinary failed invocation.
/// </remarks>
/// <remarks>
/// Resolution is deliberately reduced to match the current <see cref="IToolCatalog"/>: when the requested
/// reference does not already carry a resolved <see cref="ToolReference.Id"/>, its
/// <see cref="ToolReference.ProviderAlias"/> text is the only candidate identity this catalog can attempt to
/// resolve. A hallucinated or otherwise unregistered alias fails that lookup and reaches
/// <see cref="ToolTerminalStatus.UnknownTool"/> with an unresolved reference; it is never treated as resolved
/// merely because the text was syntactically usable as a <see cref="ToolId"/>.
/// </remarks>
public sealed class DefaultToolInvoker: IToolInvoker
{
    /// <summary>
    /// The bounds used to compile a resolved tool's declared input schema and validate one call's arguments
    /// against it, for a caller that does not explicitly supply <see cref="AgentToolsOptions.ArgumentValidationLimits"/>.
    /// </summary>
    private static readonly ToolSchemaLimits _defaultArgumentValidationLimits = new(
        maximumUtf8Bytes: 262_144, maximumDepth: 64, maximumNodes: 10_000, maximumWork: 100_000);

    private readonly IToolCatalog _catalog;
    private readonly IToolAuthorizer _authorizer;
    private readonly ILogger<DefaultToolInvoker> _logger;
    private readonly IToolSchemaEngine _schemaEngine;
    private readonly ToolSchemaLimits _argumentValidationLimits;
    private readonly ConcurrentDictionary<(ToolId Id, ToolVersion Version), ToolSchemaCompilationResult> _compiledSchemas = new();

    /// <summary>Initializes a new instance of the <see cref="DefaultToolInvoker"/> class.</summary>
    /// <param name="catalog">The catalog used to resolve a call's tool identity.</param>
    /// <param name="authorizer">The authorizer used to decide whether a resolved call may proceed.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="catalog"/> or <paramref name="authorizer"/> is null.
    /// </exception>
    public DefaultToolInvoker(IToolCatalog catalog, IToolAuthorizer authorizer)
        : this(catalog, authorizer, Microsoft.Extensions.Logging.Abstractions.NullLogger<DefaultToolInvoker>.Instance)
    {
    }

    /// <summary>Initializes an invoker with its type-specific structured logger.</summary>
    /// <param name="catalog">The catalog used to resolve a call's tool identity.</param>
    /// <param name="authorizer">The authorizer used to decide whether a resolved call may proceed.</param>
    /// <param name="logger">The logger that receives safe tool-lifecycle diagnostics.</param>
    /// <exception cref="ArgumentNullException">Any dependency is null.</exception>
    /// <remarks>
    /// Argument schema validation still runs, using an internally owned <see cref="BoundedToolSchemaEngine"/>
    /// instance and this class's own default bounds. A caller composing through <c>AddAgentTools</c> gets the
    /// application's registered, possibly replaced, <see cref="IToolSchemaEngine"/> and configurable
    /// <see cref="AgentToolsOptions.ArgumentValidationLimits"/> instead; see the five-parameter constructor.
    /// </remarks>
    public DefaultToolInvoker(
        IToolCatalog catalog,
        IToolAuthorizer authorizer,
        ILogger<DefaultToolInvoker> logger)
        : this(
            catalog,
            authorizer,
            logger,
            new BoundedToolSchemaEngine(
                TimeProvider.System,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<BoundedToolSchemaEngine>.Instance,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<CompiledToolSchema>.Instance),
            _defaultArgumentValidationLimits)
    {
    }

    /// <summary>Initializes an invoker with an explicit schema engine and argument-validation bounds.</summary>
    /// <param name="catalog">The catalog used to resolve a call's tool identity.</param>
    /// <param name="authorizer">The authorizer used to decide whether a resolved call may proceed.</param>
    /// <param name="logger">The logger that receives safe tool-lifecycle diagnostics.</param>
    /// <param name="schemaEngine">
    /// Compiles each resolved tool's declared <see cref="ToolDescriptor.InputSchema"/> once; the compiled handle
    /// is cached for the lifetime of this invoker, keyed by exact tool identity and version.
    /// </param>
    /// <param name="argumentValidationLimits">The bounds applied to both schema compilation and per-call argument validation.</param>
    /// <exception cref="ArgumentNullException">Any dependency is null.</exception>
    public DefaultToolInvoker(
        IToolCatalog catalog,
        IToolAuthorizer authorizer,
        ILogger<DefaultToolInvoker> logger,
        IToolSchemaEngine schemaEngine,
        ToolSchemaLimits argumentValidationLimits)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(authorizer);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(schemaEngine);
        ArgumentNullException.ThrowIfNull(argumentValidationLimits);

        _catalog = catalog;
        _authorizer = authorizer;
        _logger = logger;
        _schemaEngine = schemaEngine;
        _argumentValidationLimits = argumentValidationLimits;
    }

    /// <inheritdoc/>
    public async Task<ResolvedToolInvocation> InvokeAsync(ToolCallRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var context = request.Context;
        var requestedTool = request.Tool;

        // A reference that already carries a resolved identity (a caller that already knows the exact tool,
        // rather than one parsed from a model's alias) is resolved by that identity directly; otherwise the
        // provider alias is the only candidate this reduced catalog can attempt to resolve against.
        var candidateId = requestedTool.Id ?? new ToolId(requestedTool.ProviderAlias.Value);
        using var activity = AgentKitDiagnostics.Activities.StartActivity(
            AgentKitActivityNames.ExecuteTool,
            ActivityKind.Internal,
            parentContext: Activity.Current?.Context ?? default,
            tags: CreateActivityTags(request, candidateId));
        ToolLog.Started(_logger, context.ToolCallId, candidateId);

        ITool tool;
        ToolAuthorizationDecision authorization;
        ToolReference resolvedTool;
        try
        {
            if (!_catalog.TryResolve(candidateId, out var resolvedToolImpl, out var descriptor))
            {
                activity.SetFailed("unknown_tool", "unknown_tool");
                ToolMetrics.Calls.Add(1, new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, "unknown_tool"));
                ToolLog.Unknown(_logger, context.ToolCallId, candidateId);
                return new ResolvedToolInvocation(
                    requestedTool,
                    ToolResultProjectionPolicyReference.Default,
                    Rejected(ToolTerminalStatus.UnknownTool, $"Tool '{candidateId}' is not registered."));
            }

            tool = resolvedToolImpl;
            resolvedTool = new ToolReference(requestedTool.ProviderAlias, descriptor.Id, descriptor.Version);

            // Every call passes schema validation before the configured security authority, so a resolved but
            // never-validated call cannot reach ITool.InvokeAsync purely because a feature tool's own ad-hoc
            // TryParse happens not to re-implement a constraint its declared schema promises (e.g.
            // "additionalProperties": false or a numeric "minimum").
            var schemaRejection = ValidateArguments(descriptor, request.Arguments, cancellationToken);
            if (schemaRejection is not null)
            {
                activity.SetFailed(schemaRejection.Value.Metric, schemaRejection.Value.Metric);
                ToolMetrics.Calls.Add(1, new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, schemaRejection.Value.Metric));
                ToolLog.Failed(_logger, context.ToolCallId, candidateId, schemaRejection.Value.Metric);
                return new ResolvedToolInvocation(
                    resolvedTool,
                    ToolResultProjectionPolicyReference.Default,
                    Rejected(schemaRejection.Value.Status, schemaRejection.Value.SafeMessage));
            }

            authorization = await _authorizer.AuthorizeAsync(
                new ToolAuthorizationRequest(request.Context, descriptor), cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            activity.SetFailed("cancelled", "cancellation");
            ToolMetrics.Calls.Add(1, new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, "cancelled"));
            ToolLog.Cancelled(_logger, context.ToolCallId, candidateId);
            throw;
        }
        catch (Exception exception)
        {
            // Resolution and authorization run before any effect; a fault there is a pre-invocation terminal
            // failure (definitely not performed), never an exception that would orphan the model's tool call.
            activity.SetFailed("failed", exception.GetType().FullName ?? exception.GetType().Name);
            ToolMetrics.Calls.Add(1, new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, "failed"));
            ToolLog.Failed(_logger, context.ToolCallId, candidateId, exception.GetType().FullName ?? exception.GetType().Name);
            return new ResolvedToolInvocation(
                requestedTool,
                ToolResultProjectionPolicyReference.Default,
                new ToolInvocationResult(
                    new ToolCallOutcome(
                        ToolCallOutcomeKind.Failed, ToolTerminalStatus.InvocationFailed, SideEffectCertainty.DefinitelyNotPerformed,
                        false, $"Tool '{candidateId}' could not be resolved or authorized.", ExtensionData.Empty),
                    []));
        }

        if (authorization is ToolAuthorizationDenied denied)
        {
            activity.SetFailed("denied", "permission_denied");
            ToolMetrics.Calls.Add(1, new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, "denied"));
            ToolLog.Denied(_logger, context.ToolCallId, candidateId);
            return new ResolvedToolInvocation(
                resolvedTool,
                ToolResultProjectionPolicyReference.Default,
                Rejected(ToolTerminalStatus.Denied, denied.SafeMessage));
        }

        var invocationRequest = new ToolInvocationRequest(request.Context, request.Arguments, request.RequestedAt);

        try
        {
            var result = await tool.InvokeAsync(invocationRequest, cancellationToken).ConfigureAwait(false);
            var outcome = result.Outcome.Kind.ToString();
            if (result.Outcome.Kind == ToolCallOutcomeKind.Success)
            {
                activity.SetSuccessful(outcome);
            }
            else
            {
                activity.SetFailed(outcome, outcome);
            }

            ToolMetrics.Calls.Add(1, new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome));
            ToolLog.Completed(_logger, context.ToolCallId, candidateId, result.Outcome.Kind);
            return new ResolvedToolInvocation(resolvedTool, ToolResultProjectionPolicyReference.Default, result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            activity.SetFailed("cancelled", "cancellation");
            ToolMetrics.Calls.Add(1, new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, "cancelled"));
            ToolLog.Cancelled(_logger, context.ToolCallId, candidateId);
            throw;
        }
        catch (Exception exception)
        {
            activity.SetFailed("failed", exception.GetType().FullName ?? exception.GetType().Name);
            ToolMetrics.Calls.Add(1, new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, "failed"));
            ToolLog.Failed(
                _logger,
                context.ToolCallId,
                candidateId,
                exception.GetType().FullName ?? exception.GetType().Name);
            return new ResolvedToolInvocation(
                resolvedTool,
                ToolResultProjectionPolicyReference.Default,
                Failed($"Tool '{candidateId}' threw an unhandled exception during invocation."));
        }
    }

    /// <summary>
    /// Compiles (once, then from cache) and applies the resolved tool's declared canonical input schema against
    /// one call's arguments.
    /// </summary>
    /// <param name="descriptor">The exact descriptor captured alongside the resolved tool.</param>
    /// <param name="arguments">The call's raw canonical arguments.</param>
    /// <param name="cancellationToken">Propagates cancellation to compilation and validation.</param>
    /// <returns>
    /// <see langword="null"/> when the arguments satisfy the schema and invocation may proceed; otherwise the
    /// typed rejection evidence to report instead of invoking the tool.
    /// </returns>
    private (ToolTerminalStatus Status, string SafeMessage, string Metric)? ValidateArguments(
        ToolDescriptor descriptor, JsonElement arguments, CancellationToken cancellationToken)
    {
        var compilation = _compiledSchemas.GetOrAdd(
            (descriptor.Id, descriptor.Version),
            _ => _schemaEngine.Compile(descriptor.InputSchema, _argumentValidationLimits, cancellationToken));

        if (compilation is not ToolSchemaCompiled compiled)
        {
            // The tool's own declared schema cannot be validated by the configured engine (an unsupported
            // dialect/keyword, a malformed schema, or one that exceeds compilation resource limits). This is a
            // tool-definition problem, never evidence about whether these particular arguments are valid.
            return (
                ToolTerminalStatus.Unsupported,
                "The tool's declared input schema could not be compiled by the configured validation engine.",
                "schema_unsupported");
        }

        return compiled.Schema.Validate(arguments, _argumentValidationLimits, cancellationToken) switch
        {
            ToolSchemaValidationResult.Valid => null,
            ToolSchemaValidationResult.ResourceLimitExceeded => (
                ToolTerminalStatus.ResourceLimitExceeded,
                "The arguments could not be validated within the configured resource limits.",
                "resource_limit_exceeded"),
            ToolSchemaValidationResult.Invalid => (
                ToolTerminalStatus.InvalidArguments,
                "The arguments did not satisfy the tool's declared schema.",
                "invalid_arguments"),
            _ => (
                ToolTerminalStatus.InvalidArguments,
                "The arguments did not satisfy the tool's declared schema.",
                "invalid_arguments"),
        };
    }

    private static ActivityTagsCollection CreateActivityTags(ToolCallRequest request, ToolId candidateId)
    {
        Debug.Assert(request is not null, "A validated request is required to create tool activity tags.");
        var tags = new ActivityTagsCollection
        {
            { AgentKitTagNames.GenAiOperationName, AgentKitActivityNames.ExecuteTool },
            { AgentKitTagNames.AgentId, request.Context.AgentId.ToString() },
            { AgentKitTagNames.SessionId, request.Context.SessionId?.ToString() },
            { AgentKitTagNames.ToolCallId, request.Context.ToolCallId.ToString() },
            { AgentKitTagNames.ToolName, candidateId.ToString() },
            { AgentKitTagNames.OperationId, request.Context.Correlation.OperationId.ToString() },
        };

        if (request.Context.Correlation is InRunOperationCorrelation runCorrelation)
        {
            tags.Add(AgentKitTagNames.RunId, runCorrelation.RunId.ToString());
            tags.Add(AgentKitTagNames.TurnId, runCorrelation.TurnId?.ToString());
        }

        return tags;
    }

    private static ToolInvocationResult Rejected(ToolTerminalStatus status, string safeMessage) => new(
        new ToolCallOutcome(ToolCallOutcomeKind.Rejected, status, SideEffectCertainty.DefinitelyNotPerformed, false, safeMessage, ExtensionData.Empty),
        []);

    private static ToolInvocationResult Failed(string safeMessage) => new(
        new ToolCallOutcome(ToolCallOutcomeKind.Failed, ToolTerminalStatus.InvocationFailed, SideEffectCertainty.Unknown, false, safeMessage, ExtensionData.Empty),
        []);
}
