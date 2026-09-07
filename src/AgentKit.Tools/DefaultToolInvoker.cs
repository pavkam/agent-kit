// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

/// <summary>
/// The default <see cref="IToolInvoker"/>: resolves a call against the
/// registered <see cref="IToolCatalog"/>, authorizes it through the
/// registered <see cref="IToolAuthorizer"/>, and invokes the resolved tool,
/// translating every reachable failure into an ordinary
/// <see cref="ToolInvocationResult"/>.
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
public sealed class DefaultToolInvoker: IToolInvoker
{
    private readonly IToolCatalog _catalog;
    private readonly IToolAuthorizer _authorizer;
    private readonly ILogger<DefaultToolInvoker> _logger;

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
    public DefaultToolInvoker(
        IToolCatalog catalog,
        IToolAuthorizer authorizer,
        ILogger<DefaultToolInvoker> logger)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(authorizer);
        ArgumentNullException.ThrowIfNull(logger);

        _catalog = catalog;
        _authorizer = authorizer;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ToolInvocationResult> InvokeAsync(ToolCallRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var context = request.Context;
        using var activity = AgentKitDiagnostics.Activities.StartActivity(
            AgentKitActivityNames.ExecuteTool,
            ActivityKind.Internal,
            parentContext: Activity.Current?.Context ?? default,
            tags: CreateActivityTags(request));
        ToolLog.Started(_logger, context.ToolCallId, request.ToolId);

        if (!_catalog.TryResolve(request.ToolId, out var tool))
        {
            activity.SetFailed("unknown_tool", "unknown_tool");
            ToolMetrics.Calls.Add(1, new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, "unknown_tool"));
            ToolLog.Unknown(_logger, context.ToolCallId, request.ToolId);
            return Rejected($"Tool '{request.ToolId}' is not registered.");
        }

        var authorization = await _authorizer.AuthorizeAsync(
            new ToolAuthorizationRequest(request.Context, tool.Descriptor), cancellationToken).ConfigureAwait(false);

        if (authorization is ToolAuthorizationDenied denied)
        {
            activity.SetFailed("denied", "permission_denied");
            ToolMetrics.Calls.Add(1, new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, "denied"));
            ToolLog.Denied(_logger, context.ToolCallId, request.ToolId);
            return Rejected(denied.SafeMessage);
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
            ToolLog.Completed(_logger, context.ToolCallId, request.ToolId, result.Outcome.Kind);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            activity.SetFailed("cancelled", "cancellation");
            ToolMetrics.Calls.Add(1, new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, "cancelled"));
            ToolLog.Cancelled(_logger, context.ToolCallId, request.ToolId);
            throw;
        }
        catch (Exception exception)
        {
            activity.SetFailed("failed", exception.GetType().FullName ?? exception.GetType().Name);
            ToolMetrics.Calls.Add(1, new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, "failed"));
            ToolLog.Failed(
                _logger,
                context.ToolCallId,
                request.ToolId,
                exception.GetType().FullName ?? exception.GetType().Name);
            return Failed($"Tool '{request.ToolId}' threw an unhandled exception during invocation.");
        }
    }

    private static ActivityTagsCollection CreateActivityTags(ToolCallRequest request)
    {
        Debug.Assert(request is not null, "A validated request is required to create tool activity tags.");
        var tags = new ActivityTagsCollection
        {
            { AgentKitTagNames.GenAiOperationName, AgentKitActivityNames.ExecuteTool },
            { AgentKitTagNames.AgentId, request.Context.AgentId.ToString() },
            { AgentKitTagNames.SessionId, request.Context.SessionId?.ToString() },
            { AgentKitTagNames.ToolCallId, request.Context.ToolCallId.ToString() },
            { AgentKitTagNames.ToolName, request.ToolId.ToString() },
            { AgentKitTagNames.OperationId, request.Context.Correlation.OperationId.ToString() },
        };

        if (request.Context.Correlation is InRunOperationCorrelation runCorrelation)
        {
            tags.Add(AgentKitTagNames.RunId, runCorrelation.RunId.ToString());
            tags.Add(AgentKitTagNames.TurnId, runCorrelation.TurnId?.ToString());
        }

        return tags;
    }

    private static ToolInvocationResult Rejected(string safeMessage) => new(
        new ToolCallOutcome(ToolCallOutcomeKind.Rejected, safeMessage, ExtensionData.Empty),
        []);

    private static ToolInvocationResult Failed(string safeMessage) => new(
        new ToolCallOutcome(ToolCallOutcomeKind.Failed, safeMessage, ExtensionData.Empty),
        []);
}
