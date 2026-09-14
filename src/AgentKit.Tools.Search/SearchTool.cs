// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Search;

/// <summary>Searches bounded workspace content through an authorized no-follow host capability.</summary>
public sealed class SearchTool: ITool
{
    /// <summary>The stable identity under which the tool is registered.</summary>
    public static readonly ToolId Id = new("search");

    private static readonly JsonElement _inputSchema = JsonDocument.Parse(
        """
        {
          "type": "object",
          "properties": {
            "pattern": { "type": "string", "description": "Text to find inside file contents: a literal substring, or a .NET regular expression when 'regex' is true. Not a path glob." },
            "regex": { "type": "boolean", "default": true, "description": "Whether 'pattern' is a .NET non-backtracking regular expression (true) or a literal substring (false)." },
            "path_pattern": { "type": "string", "default": "**/*", "description": "Relative simple glob restricting which file paths are searched, using *, ?, and ** path segments." },
            "base_path": { "type": ["string", "null"], "description": "Optional directory relative to the workspace root." },
            "case_sensitive": { "type": "boolean", "default": true },
            "include_hidden": { "type": "boolean", "default": false },
            "maximum_depth": { "type": "integer", "minimum": 1 },
            "maximum_files": { "type": "integer", "minimum": 1 },
            "maximum_bytes": { "type": "integer", "minimum": 1 },
            "maximum_matches": { "type": "integer", "minimum": 1 },
            "maximum_line_bytes": { "type": "integer", "minimum": 1 },
            "maximum_duration_ms": { "type": "integer", "minimum": 1 }
          },
          "required": ["pattern"],
          "additionalProperties": false
        }
        """).RootElement;

    private readonly IFileContentSearcher _searcher;
    private readonly ISecurityAuthority _securityAuthority;
    private readonly IIdentifierGenerator<SecurityRequestId> _requestIds;
    private readonly TimeProvider _timeProvider;
    private readonly SearchToolOptions _options;

    /// <summary>Initializes a search tool.</summary>
    /// <param name="searcher">The narrow host search capability.</param>
    /// <param name="securityAuthority">The system-wide authority.</param>
    /// <param name="requestIds">The security-request identity generator.</param>
    /// <param name="timeProvider">The deterministic clock.</param>
    /// <param name="options">The validated search bounds.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A configured default or ceiling is invalid.</exception>
    public SearchTool(
        IFileContentSearcher searcher,
        ISecurityAuthority securityAuthority,
        IIdentifierGenerator<SecurityRequestId> requestIds,
        TimeProvider timeProvider,
        IOptions<SearchToolOptions> options)
    {
        ArgumentNullException.ThrowIfNull(searcher);
        ArgumentNullException.ThrowIfNull(securityAuthority);
        ArgumentNullException.ThrowIfNull(requestIds);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        ValidateOptions(options.Value);
        _searcher = searcher;
        _securityAuthority = securityAuthority;
        _requestIds = requestIds;
        _timeProvider = timeProvider;
        _options = options.Value;
    }

    /// <inheritdoc/>
    public ToolDescriptor Descriptor { get; } = new(
        Id,
        new ToolVersion("1.0"),
        "search",
        "Searches strict UTF-8 workspace files using literal text or .NET non-backtracking regex. Paths use simple-glob v1; ordering is ordinal; symlinks, binary files, and ambient ignore files are excluded.",
        new JsonSchema(new JsonSchemaDialectId("https://json-schema.org/draft/2020-12/schema"), _inputSchema),
        outputSchema: null,
        new ToolEffects(ToolEffect.ReadOnly, idempotency: null, requiredResourceKinds: null),
        new ToolExecutionHints(ToolSchedulingMode.Unspecified, concurrencyKey: null, expectedDuration: null, approvalMayBeCached: null),
        new ToolSourceId("agentkit.tools.search"),
        ExtensionData.Empty);

    /// <inheritdoc/>
    public async Task<ToolInvocationResult> InvokeAsync(
        ToolInvocationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!TryParse(request.Arguments, out var parsed, out var error))
        {
            return Failure(error!, "InvalidArguments", [], ToolTerminalStatus.InvalidArguments, SideEffectCertainty.DefinitelyNotPerformed);
        }

        var context = request.Context;
        var fingerprint = Fingerprint(parsed);
        var decision = await _securityAuthority.AuthorizeAsync(
            new SecurityRequest(
                _requestIds.Create(),
                new SecurityAuthorizationScope(context.AgentId, context.SessionId, context.Correlation),
                context.ToolCallId,
                context.Identity,
                _searcher.SecurityAudience,
                SecurityOperationKind.FileSearch,
                SecurityEffect.Observe,
                [FileSearchSecurityBinding.Resource(parsed.BasePath)],
                fingerprint,
                _timeProvider.GetUtcNow().AddMinutes(1)),
            cancellationToken).ConfigureAwait(false);
        if (decision is SecurityDenied denied)
        {
            return Failure(denied.Denial.SafeMessage, "Denied", [], ToolTerminalStatus.Denied, SideEffectCertainty.DefinitelyNotPerformed);
        }

        if (decision is not SecurityAllowed allowed)
        {
            return Failure("The security authority returned an unsupported decision.", "Denied", [], ToolTerminalStatus.Denied, SideEffectCertainty.DefinitelyNotPerformed);
        }

        var result = await _searcher.SearchAsync(ToRequest(parsed, allowed.Grant), cancellationToken).ConfigureAwait(false);
        var json = JsonSerializer.Serialize(new
        {
            status = result.Status.ToString(),
            matches = result.Matches.Select(static match => new
            {
                path = match.Path.Value,
                content_fingerprint = match.ContentFingerprint.Value,
                line_number = match.LineNumber,
                line_byte_offset = match.LineByteOffset,
                match_byte_offset = match.MatchByteOffset,
                match_byte_length = match.MatchByteLength,
                line_projection_byte_offset = match.LineProjectionByteOffset,
                line_text = match.LineText,
                line_text_truncated = match.LineTextTruncated,
            }),
            visited_files = result.VisitedFiles,
            visited_bytes = result.VisitedBytes,
            complete = result.Complete,
        });
        var content = ImmutableArray.Create<ContentPart>(
            new TextPart(json, TextSemantics.Code, ExtensionData.Empty));
        return result.Status is FileSearchStatus.Success or FileSearchStatus.NoMatches
            ? new ToolInvocationResult(
                new ToolCallOutcome(ToolCallOutcomeKind.Success, ToolTerminalStatus.Succeeded, SideEffectCertainty.DefinitelyPerformed, false, null, Status(result.Status.ToString())), content)
            : Failure(result.SafeMessage!, result.Status.ToString(), content, result.Status switch { FileSearchStatus.Denied => ToolTerminalStatus.Denied, FileSearchStatus.TimedOut => ToolTerminalStatus.TimedOut, FileSearchStatus.Success or FileSearchStatus.NoMatches or FileSearchStatus.NotFound or FileSearchStatus.LimitExceeded or FileSearchStatus.Failed => ToolTerminalStatus.InvocationFailed, _ => ToolTerminalStatus.InvocationFailed }, result.Status is FileSearchStatus.Denied or FileSearchStatus.NotFound ? SideEffectCertainty.DefinitelyNotPerformed : result.VisitedFiles > 0 || result.VisitedBytes > 0 ? SideEffectCertainty.PartiallyPerformed : SideEffectCertainty.Unknown);
    }

    private bool TryParse(JsonElement arguments, out ParsedArguments parsed, out string? error)
    {
        parsed = default;
        error = null;
        if (arguments.ValueKind != JsonValueKind.Object
            || !arguments.TryGetProperty("pattern", out var rawPattern)
            || rawPattern.ValueKind != JsonValueKind.String
            || !TryBoolean(arguments, "regex", true, out var regex)
            || !TryBoolean(arguments, "case_sensitive", true, out var caseSensitive)
            || !TryBoolean(arguments, "include_hidden", false, out var includeHidden))
        {
            error = "A string 'pattern' is required, and 'regex'/'case_sensitive'/'include_hidden', if given, must be booleans.";
            return false;
        }

        try
        {
            var pattern = new FileSearchPattern(
                rawPattern.GetString()!,
                regex ? FileSearchPatternKind.RegularExpression : FileSearchPatternKind.Literal);
            var pathPattern = new GlobPattern(GetString(arguments, "path_pattern", "**/*"));
            var basePath = OptionalPath(arguments, "base_path");
            if (!TryBound(arguments, "maximum_depth", _options.DefaultMaximumDepth, _options.MaximumDepth, out var depth)
                || !TryBound(arguments, "maximum_files", _options.DefaultMaximumFiles, _options.MaximumFiles, out var files)
                || !TryBound(arguments, "maximum_bytes", _options.DefaultMaximumBytes, _options.MaximumBytes, out var bytes)
                || !TryBound(arguments, "maximum_matches", _options.DefaultMaximumMatches, _options.MaximumMatches, out var matches)
                || !TryBound(arguments, "maximum_line_bytes", _options.DefaultMaximumLineBytes, _options.MaximumLineBytes, out var lineBytes)
                || !TryDuration(arguments, out var duration))
            {
                error = "Numeric bounds must be positive and within host ceilings.";
                return false;
            }

            parsed = new ParsedArguments(
                basePath, pattern, pathPattern, caseSensitive, includeHidden, depth, files, bytes, matches, lineBytes, duration);
            return true;
        }
        catch (ArgumentException exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private bool TryDuration(JsonElement arguments, out TimeSpan duration)
    {
        var fallback = checked((long) _options.DefaultMaximumDuration.TotalMilliseconds);
        var ceiling = checked((long) _options.MaximumDuration.TotalMilliseconds);
        if (!TryBound(arguments, "maximum_duration_ms", fallback, ceiling, out var milliseconds))
        {
            duration = default;
            return false;
        }

        duration = TimeSpan.FromMilliseconds(milliseconds);
        return true;
    }

    private static FileSearchRequest ToRequest(ParsedArguments value, SecurityGrant grant) => new(
        value.BasePath,
        value.Pattern,
        value.PathPattern,
        value.CaseSensitive,
        value.IncludeHidden,
        value.MaximumDepth,
        value.MaximumFiles,
        value.MaximumBytes,
        value.MaximumMatches,
        value.MaximumLineBytes,
        value.MaximumDuration,
        grant);

    private static InputFingerprint Fingerprint(ParsedArguments value) => FileSearchSecurityBinding.Fingerprint(
        value.BasePath,
        value.Pattern,
        value.PathPattern,
        value.CaseSensitive,
        value.IncludeHidden,
        value.MaximumDepth,
        value.MaximumFiles,
        value.MaximumBytes,
        value.MaximumMatches,
        value.MaximumLineBytes,
        value.MaximumDuration);

    private static FileSystemPath? OptionalPath(JsonElement arguments, string name)
    {
        return !arguments.TryGetProperty(name, out var property) || property.ValueKind == JsonValueKind.Null
            ? null
            : property.ValueKind == JsonValueKind.String
                ? new FileSystemPath(property.GetString()!)
                : throw new ArgumentException($"Property '{name}' must be a string or null.", name);
    }

    private static string GetString(JsonElement arguments, string name, string fallback) =>
        !arguments.TryGetProperty(name, out var property)
            ? fallback
            : property.ValueKind == JsonValueKind.String
                ? property.GetString()!
                : throw new ArgumentException($"Property '{name}' must be a string.", name);

    private static bool TryBoolean(JsonElement arguments, string name, bool fallback, out bool value)
    {
        if (!arguments.TryGetProperty(name, out var property))
        {
            value = fallback;
            return true;
        }

        value = property.ValueKind is JsonValueKind.True or JsonValueKind.False && property.GetBoolean();
        return property.ValueKind is JsonValueKind.True or JsonValueKind.False;
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

    private static bool TryBound(JsonElement arguments, string name, long fallback, long ceiling, out long value)
    {
        if (!arguments.TryGetProperty(name, out var property))
        {
            value = fallback;
            return true;
        }

        return property.TryGetInt64(out value) && value > 0 && value <= ceiling;
    }

    private static void ValidateOptions(SearchToolOptions value)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value.DefaultMaximumDepth, nameof(value.DefaultMaximumDepth));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value.MaximumDepth, nameof(value.MaximumDepth));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value.DefaultMaximumDepth, value.MaximumDepth, nameof(value.DefaultMaximumDepth));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value.DefaultMaximumFiles, nameof(value.DefaultMaximumFiles));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value.MaximumFiles, nameof(value.MaximumFiles));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value.DefaultMaximumFiles, value.MaximumFiles, nameof(value.DefaultMaximumFiles));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value.DefaultMaximumBytes, nameof(value.DefaultMaximumBytes));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value.MaximumBytes, nameof(value.MaximumBytes));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value.DefaultMaximumBytes, value.MaximumBytes, nameof(value.DefaultMaximumBytes));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value.DefaultMaximumMatches, nameof(value.DefaultMaximumMatches));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value.MaximumMatches, nameof(value.MaximumMatches));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value.DefaultMaximumMatches, value.MaximumMatches, nameof(value.DefaultMaximumMatches));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value.DefaultMaximumLineBytes, nameof(value.DefaultMaximumLineBytes));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value.MaximumLineBytes, nameof(value.MaximumLineBytes));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value.DefaultMaximumLineBytes, value.MaximumLineBytes, nameof(value.DefaultMaximumLineBytes));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value.DefaultMaximumDuration, TimeSpan.Zero, nameof(value.DefaultMaximumDuration));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value.MaximumDuration, TimeSpan.Zero, nameof(value.MaximumDuration));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value.DefaultMaximumDuration, value.MaximumDuration, nameof(value.DefaultMaximumDuration));
    }

    private static ExtensionData Status(string status) => new(
        ImmutableDictionary<string, ExtensionValue>.Empty.Add(
            "agentkit.search.status",
            new ExtensionValue([.. JsonSerializer.SerializeToUtf8Bytes(status)])));

    private static ToolInvocationResult Failure(
        string reason,
        string status,
        ImmutableArray<ContentPart> content, ToolTerminalStatus sourceStatus, SideEffectCertainty certainty) => new(
            new ToolCallOutcome(sourceStatus.ToOutcomeKind(), sourceStatus, certainty, false, reason, Status(status)), content);

    private readonly record struct ParsedArguments(
        FileSystemPath? BasePath,
        FileSearchPattern Pattern,
        GlobPattern PathPattern,
        bool CaseSensitive,
        bool IncludeHidden,
        int MaximumDepth,
        int MaximumFiles,
        long MaximumBytes,
        int MaximumMatches,
        int MaximumLineBytes,
        TimeSpan MaximumDuration);
}
