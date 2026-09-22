// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Glob;

using AgentKit.Tools;

/// <summary>Matches paths using AgentKit simple-glob v1 through an authorized no-follow host traversal.</summary>
public sealed class GlobTool: IToolInvoker, ITool
{
    private const int _maximumExcludedPathPatterns = 100;
    /// <summary>The stable identity under which the tool is registered.</summary>
    public static readonly ToolId Id = new("glob");

    private static readonly JsonElement _inputSchema = JsonDocument.Parse(
        """
        {
          "type": "object",
          "properties": {
            "pattern": { "type": "string", "description": "Relative simple glob using *, ?, and ** path segments." },
            "base_path": { "type": ["string", "null"], "description": "Optional directory relative to the workspace root." },
            "case_sensitive": { "type": "boolean", "default": true },
            "include_hidden": { "type": "boolean", "default": false },
            "exclude_patterns": { "type": "array", "maxItems": 100, "items": { "type": "string" }, "default": [], "description": "Simple globs excluded before traversal. A pattern ending in /** prunes its matching directory subtree." },
            "maximum_depth": { "type": "integer", "minimum": 1 },
            "maximum_visited_entries": { "type": "integer", "minimum": 1 },
            "maximum_results": { "type": "integer", "minimum": 1 }
          },
          "required": ["pattern"],
          "additionalProperties": false
        }
        """).RootElement;

    private readonly IFileGlobber _globber;
    private readonly ISecurityAuthoritySelector _authoritySelector;
    private readonly IIdentifierGenerator<SecurityRequestId> _requestIds;
    private readonly TimeProvider _timeProvider;
    private readonly GlobToolOptions _options;

    /// <summary>Initializes a glob tool.</summary>
    /// <param name="globber">The narrow host glob capability.</param>
    /// <param name="authoritySelector">The security authority selector.</param>
    /// <param name="requestIds">The security-request identity generator.</param>
    /// <param name="timeProvider">The deterministic clock.</param>
    /// <param name="options">The validated traversal options.</param>
    /// <exception cref="ArgumentNullException">Any dependency is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Any configured bound is invalid.</exception>
    public GlobTool(
        IFileGlobber globber,
        ISecurityAuthoritySelector authoritySelector,
        IIdentifierGenerator<SecurityRequestId> requestIds,
        TimeProvider timeProvider,
        IOptions<GlobToolOptions> options)
    {
        ArgumentNullException.ThrowIfNull(globber);
        ArgumentNullException.ThrowIfNull(authoritySelector);
        ArgumentNullException.ThrowIfNull(requestIds);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        ValidateOptions(options.Value);
        _globber = globber;
        _authoritySelector = authoritySelector;
        _requestIds = requestIds;
        _timeProvider = timeProvider;
        _options = options.Value;
    }

    /// <summary>Gets the immutable descriptor shared with registration and presentation formatting.</summary>
    public static ToolDescriptor Descriptor { get; } = new(
        Id,
        new ToolVersion("1.0"),
        "glob",
        "Matches workspace paths with AgentKit simple-glob v1. It never follows symlinks or consults ambient ignore files.",
        new JsonSchema(new JsonSchemaDialectId("https://json-schema.org/draft/2020-12/schema"), _inputSchema),
        outputSchema: null,
        new ToolEffects(ToolEffect.ReadOnly, idempotency: null, requiredResourceKinds: null),
        new ToolExecutionHints(ToolSchedulingMode.Unspecified, concurrencyKey: null, expectedDuration: null, approvalMayBeCached: null),
        new ToolSourceId("agentkit.tools.glob"),
        ExtensionData.Empty);

    internal static ToolDescriptor PresentationDescriptor => Descriptor;

    /// <summary>Gets the default toolset publication selecting this tool from the application tool source.</summary>
    public static ToolsetPublication DefaultToolset { get; } = new(
        new ToolsetKey("agentkit.tools.glob"),
        new ToolsetVersion(1),
        new ToolExecutionPolicyReference(new ToolExecutionPolicyKey("standard"), new ToolExecutionPolicyVersion(1)),
        [new ToolsetSourceSelection(ApplicationToolSources.Default)],
        [new ToolAliasAssignment(new ToolAlias("glob"), new ToolIdentity(Id, Descriptor.Version))]);

    /// <inheritdoc/>
    ToolDescriptor ITool.Descriptor => Descriptor;

    /// <inheritdoc/>
    public ValueTask<ToolInvocationResult> InvokeAsync(
        ToolInvocationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var authorization = context.InvocationGrant.Authorization
            ?? throw new InvalidOperationException("Tool invocations require grants that retain complete authorization evidence.");
        return InvokeCoreAsync(authorization, context.CallId, context.Arguments, cancellationToken);
    }

    /// <inheritdoc/>
    [Obsolete("Legacy host surface.")]

    public Task<ToolInvocationResult> InvokeAsync(
        ToolInvocationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return InvokeCoreAsync(
            request.Context.Authorization,
            request.Context.ToolCallId,
            request.Arguments,
            cancellationToken).AsTask();
    }

    private async ValueTask<ToolInvocationResult> InvokeCoreAsync(
        SecurityAuthorizationContext authorization,
        ToolCallId callId,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        if (!TryParse(arguments, out var parsed, out var error))
        {
            return ProjectFailure(error!, "InvalidArguments", [], ToolTerminalStatus.InvalidArguments, SideEffectCertainty.DefinitelyNotPerformed);
        }

        var activated = await _authoritySelector.SelectAsync(authorization, cancellationToken).ConfigureAwait(false);
        if (activated is not SecurityAuthoritySelected selected || selected.Authorization != authorization)
        {
            return ProjectFailure("The captured security authority is unavailable.", "Denied", [], ToolTerminalStatus.Denied, SideEffectCertainty.DefinitelyNotPerformed);
        }

        var decision = await selected.Authority.AuthorizeAsync(
            new SecurityRequest(
                _requestIds.Create(),
                authorization.Scope,
                callId,
                authorization.Identity,
                authorization,
                _globber.SecurityAudience,
                SecurityOperationKind.DirectoryRead,
                SecurityEffect.Observe,
                [GlobSecurityBinding.Resource(parsed.BasePath)],
                GlobSecurityBinding.Fingerprint(
                    parsed.BasePath,
                    parsed.Pattern,
                    parsed.CaseSensitive,
                    parsed.IncludeHidden,
                    parsed.MaximumDepth,
                    parsed.MaximumVisitedEntries,
                    parsed.MaximumResults,
                    parsed.ExcludedPathPatterns),
                _timeProvider.GetUtcNow().AddMinutes(1)),
            hooks: null,
            cancellationToken).ConfigureAwait(false);
        if (decision is SecurityDenied denied)
        {
            return ProjectFailure(denied.Denial.SafeMessage, "Denied", [], ToolTerminalStatus.Denied, SideEffectCertainty.DefinitelyNotPerformed);
        }

        if (decision is not SecurityAllowed allowed)
        {
            return ProjectFailure("The security authority returned an unsupported decision.", "Denied", [], ToolTerminalStatus.Denied, SideEffectCertainty.DefinitelyNotPerformed);
        }

        var result = await _globber.GlobAsync(
            new GlobRequest(
                parsed.BasePath,
                parsed.Pattern,
                parsed.CaseSensitive,
                parsed.IncludeHidden,
                parsed.MaximumDepth,
                parsed.MaximumVisitedEntries,
                parsed.MaximumResults,
                allowed.Grant)
            {
                ExcludedPathPatterns = parsed.ExcludedPathPatterns,
            },
            cancellationToken).ConfigureAwait(false);
        var json = JsonSerializer.Serialize(new
        {
            status = result.Status.ToString(),
            matches = result.Matches.Select(static path => path.Value),
            visited_entries = result.VisitedEntries,
            complete = result.Complete,
        });
        var content = ImmutableArray.Create<ContentPart>(new TextPart(json, TextSemantics.Code, ExtensionData.Empty));
        return result.Status is GlobStatus.Success or GlobStatus.NoMatches
            ? new ToolInvocationResult(
                new ToolCallOutcome(ToolCallOutcomeKind.Success, ToolTerminalStatus.Succeeded, SideEffectCertainty.DefinitelyPerformed, false, null, StatusExtensions(result.Status.ToString())), content)
            : ProjectFailure(result.SafeMessage!, result.Status.ToString(), content, result.Status switch { GlobStatus.Denied => ToolTerminalStatus.Denied, GlobStatus.Success or GlobStatus.NoMatches or GlobStatus.NotFound or GlobStatus.LimitExceeded or GlobStatus.Failed => ToolTerminalStatus.InvocationFailed, _ => ToolTerminalStatus.InvocationFailed }, result.Status is GlobStatus.Denied or GlobStatus.NotFound ? SideEffectCertainty.DefinitelyNotPerformed : result.VisitedEntries > 0 ? SideEffectCertainty.PartiallyPerformed : SideEffectCertainty.Unknown);
    }

    private bool TryParse(JsonElement arguments, out ParsedArguments parsed, out string? error)
    {
        parsed = default;
        error = null;
        if (arguments.ValueKind != JsonValueKind.Object
            || !arguments.TryGetProperty("pattern", out var patternProperty)
            || patternProperty.ValueKind != JsonValueKind.String)
        {
            error = "A string property 'pattern' is required.";
            return false;
        }

        GlobPattern pattern;
        try
        {
            pattern = new GlobPattern(patternProperty.GetString()!);
        }
        catch (ArgumentException exception)
        {
            error = $"Invalid pattern: {exception.Message}";
            return false;
        }

        FileSystemPath? basePath = null;
        if (arguments.TryGetProperty("base_path", out var baseProperty) && baseProperty.ValueKind != JsonValueKind.Null)
        {
            if (baseProperty.ValueKind != JsonValueKind.String)
            {
                error = "Property 'base_path' must be a string or null.";
                return false;
            }

            try
            {
                basePath = new FileSystemPath(baseProperty.GetString()!);
            }
            catch (ArgumentException exception)
            {
                error = $"Invalid base path: {exception.Message}";
                return false;
            }
        }

        if (!TryBoolean(arguments, "case_sensitive", true, out var caseSensitive)
            || !TryBoolean(arguments, "include_hidden", false, out var includeHidden)
            || !TryBound(arguments, "maximum_depth", _options.DefaultMaximumDepth, _options.MaximumDepth, out var depth)
            || !TryBound(
                arguments,
                "maximum_visited_entries",
                _options.DefaultMaximumVisitedEntries,
                _options.MaximumVisitedEntries,
                out var visited)
            || !TryBound(arguments, "maximum_results", _options.DefaultMaximumResults, _options.MaximumResults, out var results)
            || !TryExcludedPathPatterns(arguments, out var excludedPathPatterns))
        {
            error = "Boolean options must be booleans and numeric bounds must be positive and within host ceilings.";
            return false;
        }

        parsed = new ParsedArguments(
            basePath, pattern, caseSensitive, includeHidden, depth, visited, results, excludedPathPatterns);
        return true;
    }

    private static bool TryExcludedPathPatterns(
        JsonElement arguments,
        out ImmutableArray<GlobPattern> excludedPathPatterns)
    {
        if (!arguments.TryGetProperty("exclude_patterns", out var property))
        {
            excludedPathPatterns = [];
            return true;
        }

        if (property.ValueKind != JsonValueKind.Array || property.GetArrayLength() > _maximumExcludedPathPatterns)
        {
            excludedPathPatterns = [];
            return false;
        }

        var builder = ImmutableArray.CreateBuilder<GlobPattern>(property.GetArrayLength());
        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                excludedPathPatterns = [];
                return false;
            }

            try
            {
                builder.Add(new GlobPattern(item.GetString()!));
            }
            catch (ArgumentException)
            {
                excludedPathPatterns = [];
                return false;
            }
        }

        excludedPathPatterns = builder.MoveToImmutable();
        return true;
    }

    private static bool TryBoolean(JsonElement arguments, string name, bool fallback, out bool value)
    {
        if (!arguments.TryGetProperty(name, out var property))
        {
            value = fallback;
            return true;
        }

        if (property.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            value = property.GetBoolean();
            return true;
        }

        value = default;
        return false;
    }

    private static bool TryBound(JsonElement arguments, string name, int fallback, int ceiling, out int value)
    {
        if (!arguments.TryGetProperty(name, out var property))
        {
            value = fallback;
            return true;
        }

        value = 0;
        return property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out value) && value > 0 && value <= ceiling;
    }

    private static void ValidateOptions(GlobToolOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.DefaultMaximumDepth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaximumDepth);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(options.DefaultMaximumDepth, options.MaximumDepth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.DefaultMaximumVisitedEntries);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaximumVisitedEntries);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            options.DefaultMaximumVisitedEntries, options.MaximumVisitedEntries);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.DefaultMaximumResults);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaximumResults);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(options.DefaultMaximumResults, options.MaximumResults);
    }

    private static ExtensionData StatusExtensions(string status) => new(
        ImmutableDictionary<string, ExtensionValue>.Empty.Add(
            "agentkit.glob.status",
            new ExtensionValue([.. JsonSerializer.SerializeToUtf8Bytes(status)])));

    private static ToolInvocationResult ProjectFailure(
        string reason,
        string status,
        ImmutableArray<ContentPart> content, ToolTerminalStatus sourceStatus, SideEffectCertainty certainty) => new(
            new ToolCallOutcome(sourceStatus.ToOutcomeKind(), sourceStatus, certainty, false, reason, StatusExtensions(status)), content);

    private readonly record struct ParsedArguments(
        FileSystemPath? BasePath,
        GlobPattern Pattern,
        bool CaseSensitive,
        bool IncludeHidden,
        int MaximumDepth,
        int MaximumVisitedEntries,
        int MaximumResults,
        ImmutableArray<GlobPattern> ExcludedPathPatterns);
}
