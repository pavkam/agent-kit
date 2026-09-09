// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Glob;

/// <summary>Matches paths using AgentKit simple-glob v1 through an authorized no-follow host traversal.</summary>
public sealed class GlobTool: ITool
{
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
            "maximum_depth": { "type": "integer", "minimum": 1 },
            "maximum_visited_entries": { "type": "integer", "minimum": 1 },
            "maximum_results": { "type": "integer", "minimum": 1 }
          },
          "required": ["pattern"],
          "additionalProperties": false
        }
        """).RootElement;

    private readonly IFileGlobber _globber;
    private readonly ISecurityAuthority _securityAuthority;
    private readonly IIdentifierGenerator<SecurityRequestId> _requestIds;
    private readonly TimeProvider _timeProvider;
    private readonly GlobToolOptions _options;

    /// <summary>Initializes a glob tool.</summary>
    /// <param name="globber">The narrow host glob capability.</param>
    /// <param name="securityAuthority">The system-wide authority.</param>
    /// <param name="requestIds">The security-request identity generator.</param>
    /// <param name="timeProvider">The deterministic clock.</param>
    /// <param name="options">The validated traversal options.</param>
    /// <exception cref="ArgumentNullException">Any dependency is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Any configured bound is invalid.</exception>
    public GlobTool(
        IFileGlobber globber,
        ISecurityAuthority securityAuthority,
        IIdentifierGenerator<SecurityRequestId> requestIds,
        TimeProvider timeProvider,
        IOptions<GlobToolOptions> options)
    {
        ArgumentNullException.ThrowIfNull(globber);
        ArgumentNullException.ThrowIfNull(securityAuthority);
        ArgumentNullException.ThrowIfNull(requestIds);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        ValidateOptions(options.Value);
        _globber = globber;
        _securityAuthority = securityAuthority;
        _requestIds = requestIds;
        _timeProvider = timeProvider;
        _options = options.Value;
    }

    /// <inheritdoc/>
    public ToolDescriptor Descriptor { get; } = new(
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

    /// <inheritdoc/>
    public async Task<ToolInvocationResult> InvokeAsync(
        ToolInvocationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!TryParse(request.Arguments, out var parsed, out var error))
        {
            return ProjectFailure(error!, "InvalidArguments", []);
        }

        var context = request.Context;
        var decision = await _securityAuthority.AuthorizeAsync(
            new SecurityRequest(
                _requestIds.Create(),
                new SecurityAuthorizationScope(context.AgentId, context.SessionId, context.Correlation),
                context.ToolCallId,
                context.Identity,
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
                    parsed.MaximumResults),
                _timeProvider.GetUtcNow().AddMinutes(1)),
            cancellationToken).ConfigureAwait(false);
        if (decision is SecurityDenied denied)
        {
            return ProjectFailure(denied.Denial.SafeMessage, "Denied", []);
        }

        if (decision is not SecurityAllowed allowed)
        {
            return ProjectFailure("The security authority returned an unsupported decision.", "Denied", []);
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
                allowed.Grant),
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
                new ToolCallOutcome(ToolCallOutcomeKind.Success, null, StatusExtensions(result.Status.ToString())), content)
            : ProjectFailure(result.SafeMessage!, result.Status.ToString(), content);
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
            || !TryBound(arguments, "maximum_results", _options.DefaultMaximumResults, _options.MaximumResults, out var results))
        {
            error = "Boolean options must be booleans and numeric bounds must be positive and within host ceilings.";
            return false;
        }

        parsed = new ParsedArguments(basePath, pattern, caseSensitive, includeHidden, depth, visited, results);
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

        return property.TryGetInt32(out value) && value > 0 && value <= ceiling;
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
        ImmutableArray<ContentPart> content) => new(
            new ToolCallOutcome(ToolCallOutcomeKind.Failed, reason, StatusExtensions(status)), content);

    private readonly record struct ParsedArguments(
        FileSystemPath? BasePath,
        GlobPattern Pattern,
        bool CaseSensitive,
        bool IncludeHidden,
        int MaximumDepth,
        int MaximumVisitedEntries,
        int MaximumResults);
}
