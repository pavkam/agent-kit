// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Language;

/// <summary>Exposes bounded read-only language intelligence through one explicit multi-operation tool.</summary>
public sealed class LanguageTool: ITool
{
    private static readonly JsonElement _inputSchema = JsonDocument.Parse(
        """
        {
          "type": "object",
          "properties": {
            "action": {
              "type": "string",
              "enum": ["diagnostics", "hover", "definitions", "implementations", "references", "document_symbols", "workspace_symbols"]
            },
            "path": { "type": ["string", "null"] },
            "line": { "type": ["integer", "null"], "minimum": 1 },
            "character": { "type": ["integer", "null"], "minimum": 1 },
            "query": { "type": ["string", "null"] },
            "maximum_results": { "type": "integer", "minimum": 1 },
            "timeout_ms": { "type": "integer", "minimum": 1 }
          },
          "required": ["action"],
          "additionalProperties": false
        }
        """).RootElement;

    private readonly ILanguageIntelligenceService _service;
    private readonly ISecurityAuthority _securityAuthority;
    private readonly IIdentifierGenerator<SecurityRequestId> _securityRequestIds;
    private readonly IIdentifierGenerator<LanguageQueryId> _queryIds;
    private readonly TimeProvider _timeProvider;
    private readonly int _defaultMaximumResults;
    private readonly int _maximumResults;
    private readonly TimeSpan _defaultTimeout;
    private readonly TimeSpan _maximumTimeout;
    private readonly int _maximumQueryCharacters;
    private readonly int _maximumTextCharacters;

    /// <summary>The stable tool identity.</summary>
    public static readonly ToolId Id = new("language");

    /// <summary>Initializes the language tool over one selected provider and the system-wide authority.</summary>
    /// <param name="service">The protected language-intelligence provider.</param>
    /// <param name="securityAuthority">The system-wide security authority.</param>
    /// <param name="securityRequestIds">The replaceable security-request identity source.</param>
    /// <param name="queryIds">The replaceable language-query identity source.</param>
    /// <param name="timeProvider">The deterministic security-deadline clock.</param>
    /// <param name="options">The captured model-facing bounds.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A configured default or ceiling is invalid.</exception>
    public LanguageTool(
        ILanguageIntelligenceService service,
        ISecurityAuthority securityAuthority,
        IIdentifierGenerator<SecurityRequestId> securityRequestIds,
        IIdentifierGenerator<LanguageQueryId> queryIds,
        TimeProvider timeProvider,
        IOptions<LanguageToolOptions> options)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(securityAuthority);
        ArgumentNullException.ThrowIfNull(securityRequestIds);
        ArgumentNullException.ThrowIfNull(queryIds);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        ValidateOptions(options.Value);
        _service = service;
        _securityAuthority = securityAuthority;
        _securityRequestIds = securityRequestIds;
        _queryIds = queryIds;
        _timeProvider = timeProvider;
        _defaultMaximumResults = options.Value.DefaultMaximumResults;
        _maximumResults = options.Value.MaximumResults;
        _defaultTimeout = options.Value.DefaultTimeout;
        _maximumTimeout = options.Value.MaximumTimeout;
        _maximumQueryCharacters = options.Value.MaximumQueryCharacters;
        _maximumTextCharacters = options.Value.MaximumTextCharacters;
    }

    /// <inheritdoc/>
    public ToolDescriptor Descriptor { get; } = new(
        Id,
        new ToolVersion("1.0"),
        "language",
        "Queries diagnostics, hover, definitions, implementations, references, and symbols from the selected language service. Input positions and projected ranges are one-based; providers use zero-based UTF-16 positions internally.",
        _inputSchema,
        ToolEffect.ReadOnly,
        ExtensionData.Empty);

    /// <inheritdoc/>
    public async Task<ToolInvocationResult> InvokeAsync(
        ToolInvocationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!TryParse(request.Arguments, out var parsed, out var error))
        {
            return Failure(error!, "InvalidArguments", []);
        }

        var queryId = _queryIds.Create();
        var resource = LanguageSecurityBinding.Resource(parsed.Kind, parsed.Path);
        var fingerprint = LanguageSecurityBinding.Fingerprint(
            queryId,
            parsed.Kind,
            parsed.Path,
            parsed.Position,
            parsed.Query,
            parsed.MaximumResults,
            parsed.Timeout);
        var context = request.Context;
        var decision = await _securityAuthority.AuthorizeAsync(
            new SecurityRequest(
                _securityRequestIds.Create(),
                new SecurityAuthorizationScope(context.AgentId, context.SessionId, context.Correlation),
                context.ToolCallId,
                context.Identity,
                _service.SecurityAudience,
                SecurityOperationKind.FileRead,
                SecurityEffect.Observe,
                [resource],
                fingerprint,
                _timeProvider.GetUtcNow().AddMinutes(1)),
            cancellationToken).ConfigureAwait(false);
        if (decision is SecurityDenied denied)
        {
            return Failure(denied.Denial.SafeMessage, "Denied", []);
        }

        if (decision is not SecurityAllowed allowed)
        {
            return Failure("The security authority returned an unsupported decision.", "Denied", []);
        }

        var result = await _service.QueryAsync(
            new LanguageQueryRequest(
                queryId,
                parsed.Kind,
                parsed.Path,
                parsed.Position,
                parsed.Query,
                parsed.MaximumResults,
                parsed.Timeout,
                allowed.Grant),
            cancellationToken).ConfigureAwait(false);
        var content = Project(result, parsed.MaximumResults);
        return result.Status == LanguageQueryStatus.Success
            ? new ToolInvocationResult(
                new ToolCallOutcome(ToolCallOutcomeKind.Success, null, Status(result.Status.ToString())),
                content)
            : Failure(result.SafeMessage!, result.Status.ToString(), content);
    }

    private bool TryParse(JsonElement arguments, out ParsedArguments parsed, out string? error)
    {
        parsed = default;
        error = null;
        if (arguments.ValueKind != JsonValueKind.Object
            || !arguments.TryGetProperty("action", out var action)
            || action.ValueKind != JsonValueKind.String
            || !TryKind(action.GetString()!, out var kind)
            || !TryPositiveBound(arguments, "maximum_results", _defaultMaximumResults, _maximumResults, out var maximumResults)
            || !TryTimeout(arguments, out var timeout))
        {
            error = "A supported 'action' and positive bounds within host ceilings are required.";
            return false;
        }

        try
        {
            var path = OptionalPath(arguments, "path");
            var position = OptionalPosition(arguments);
            var query = OptionalString(arguments, "query");
            if (query is { Length: > 0 } && query.Length > _maximumQueryCharacters)
            {
                error = "The workspace-symbol query exceeds the configured character boundary.";
                return false;
            }

            if (!ShapeMatches(kind, path, position, query))
            {
                error = "The path, position, and query do not match the requested language operation.";
                return false;
            }

            parsed = new ParsedArguments(kind, path, position, query, maximumResults, timeout);
            return true;
        }
        catch (ArgumentException exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private bool TryTimeout(JsonElement arguments, out TimeSpan timeout)
    {
        if (!arguments.TryGetProperty("timeout_ms", out var property))
        {
            timeout = _defaultTimeout;
            return true;
        }

        if (property.TryGetInt64(out var milliseconds)
            && milliseconds > 0
            && milliseconds <= _maximumTimeout.TotalMilliseconds)
        {
            timeout = TimeSpan.FromMilliseconds(milliseconds);
            return true;
        }

        timeout = default;
        return false;
    }

    private static bool TryKind(string value, out LanguageQueryKind kind)
    {
        kind = value switch
        {
            "diagnostics" => LanguageQueryKind.Diagnostics,
            "hover" => LanguageQueryKind.Hover,
            "definitions" => LanguageQueryKind.Definitions,
            "implementations" => LanguageQueryKind.Implementations,
            "references" => LanguageQueryKind.References,
            "document_symbols" => LanguageQueryKind.DocumentSymbols,
            "workspace_symbols" => LanguageQueryKind.WorkspaceSymbols,
            _ => default,
        };
        return value is "diagnostics" or "hover" or "definitions" or "implementations"
            or "references" or "document_symbols" or "workspace_symbols";
    }

    private static bool ShapeMatches(
        LanguageQueryKind kind,
        FileSystemPath? path,
        LanguagePosition? position,
        string? query)
    {
        var positionRequired = kind is LanguageQueryKind.Hover
            or LanguageQueryKind.Definitions
            or LanguageQueryKind.Implementations
            or LanguageQueryKind.References;
        return kind == LanguageQueryKind.WorkspaceSymbols != path.HasValue
            && positionRequired == position.HasValue
            && kind == LanguageQueryKind.WorkspaceSymbols == !string.IsNullOrWhiteSpace(query)
            && (kind == LanguageQueryKind.WorkspaceSymbols || query is null);
    }

    private static FileSystemPath? OptionalPath(JsonElement arguments, string name) =>
        !arguments.TryGetProperty(name, out var property) || property.ValueKind == JsonValueKind.Null
            ? null
            : property.ValueKind == JsonValueKind.String
                ? new FileSystemPath(property.GetString()!)
                : throw new ArgumentException($"Property '{name}' must be a string or null.", name);

    private static string? OptionalString(JsonElement arguments, string name) =>
        !arguments.TryGetProperty(name, out var property) || property.ValueKind == JsonValueKind.Null
            ? null
            : property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : throw new ArgumentException($"Property '{name}' must be a string or null.", name);

    private static LanguagePosition? OptionalPosition(JsonElement arguments)
    {
        var hasLine = arguments.TryGetProperty("line", out var line) && line.ValueKind != JsonValueKind.Null;
        var hasCharacter = arguments.TryGetProperty("character", out var character)
            && character.ValueKind != JsonValueKind.Null;
        return !hasLine && !hasCharacter
            ? null
            : RequiredPosition(line, character, hasLine, hasCharacter, nameof(arguments));
    }

    private static LanguagePosition RequiredPosition(
        JsonElement line,
        JsonElement character,
        bool hasLine,
        bool hasCharacter,
        string paramName) => hasLine
            && hasCharacter
            && line.TryGetInt32(out var oneBasedLine)
            && character.TryGetInt32(out var oneBasedCharacter)
            && oneBasedLine > 0
            && oneBasedCharacter > 0
                ? new LanguagePosition(oneBasedLine - 1, oneBasedCharacter - 1)
                : throw new ArgumentException(
                    "Properties 'line' and 'character' must both be positive one-based integers.",
                    paramName);

    private static bool TryPositiveBound(
        JsonElement arguments,
        string name,
        int fallback,
        int ceiling,
        out int value)
    {
        if (!arguments.TryGetProperty(name, out var property))
        {
            value = fallback;
            return true;
        }

        return property.TryGetInt32(out value) && value > 0 && value <= ceiling;
    }

    private ImmutableArray<ContentPart> Project(LanguageQueryResult result, int maximumResults)
    {
        var locations = result.Locations.Take(maximumResults).ToArray();
        var symbols = result.Symbols.Take(maximumResults).ToArray();
        var diagnostics = result.Diagnostics.Take(maximumResults).ToArray();
        var projectionTruncated = locations.Length != result.Locations.Length
            || symbols.Length != result.Symbols.Length
            || diagnostics.Length != result.Diagnostics.Length
            || ExceedsTextBoundary(result.HoverText)
            || ExceedsTextBoundary(result.SafeMessage)
            || locations.Any(location => ExceedsTextBoundary(location.Path.Value))
            || symbols.Any(symbol => ExceedsTextBoundary(symbol.Name)
                || ExceedsTextBoundary(symbol.Kind)
                || ExceedsTextBoundary(symbol.ContainerName)
                || ExceedsTextBoundary(symbol.Location.Path.Value))
            || diagnostics.Any(diagnostic => ExceedsTextBoundary(diagnostic.Message)
                || ExceedsTextBoundary(diagnostic.Code)
                || ExceedsTextBoundary(diagnostic.Source)
                || ExceedsTextBoundary(diagnostic.Location.Path.Value));
        var json = JsonSerializer.Serialize(new
        {
            status = result.Status.ToString(),
            action = Action(result.Kind),
            hover = Clip(result.HoverText),
            locations = locations.Select(Project),
            symbols = symbols.Select(symbol => new
            {
                name = Clip(symbol.Name),
                kind = Clip(symbol.Kind),
                container_name = Clip(symbol.ContainerName),
                location = Project(symbol.Location),
            }),
            diagnostics = diagnostics.Select(diagnostic => new
            {
                severity = diagnostic.Severity.ToString(),
                message = Clip(diagnostic.Message),
                code = Clip(diagnostic.Code),
                source = Clip(diagnostic.Source),
                location = Project(diagnostic.Location),
            }),
            complete = result.Complete && !projectionTruncated,
            projection_truncated = projectionTruncated,
            message = Clip(result.SafeMessage),
        });
        return [new TextPart(json, TextSemantics.Code, ExtensionData.Empty)];
    }

    private object Project(LanguageLocation location) => new
    {
        path = Clip(location.Path.Value),
        start_line = location.Range.Start.Line + 1,
        start_character = location.Range.Start.Character + 1,
        end_line = location.Range.End.Line + 1,
        end_character = location.Range.End.Character + 1,
        document_fingerprint = location.DocumentFingerprint?.Value,
    };

    private string? Clip(string? value) => value is { Length: var length } && length > _maximumTextCharacters
        ? value[.._maximumTextCharacters]
        : value;

    private bool ExceedsTextBoundary(string? value) => value?.Length > _maximumTextCharacters;

    private static string Action(LanguageQueryKind kind) => kind switch
    {
        LanguageQueryKind.Diagnostics => "diagnostics",
        LanguageQueryKind.Hover => "hover",
        LanguageQueryKind.Definitions => "definitions",
        LanguageQueryKind.Implementations => "implementations",
        LanguageQueryKind.References => "references",
        LanguageQueryKind.DocumentSymbols => "document_symbols",
        LanguageQueryKind.WorkspaceSymbols => "workspace_symbols",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static void ValidateOptions(LanguageToolOptions value)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value.DefaultMaximumResults, nameof(value.DefaultMaximumResults));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value.MaximumResults, nameof(value.MaximumResults));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            value.DefaultMaximumResults, value.MaximumResults, nameof(value.DefaultMaximumResults));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value.DefaultTimeout, TimeSpan.Zero, nameof(value.DefaultTimeout));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value.MaximumTimeout, TimeSpan.Zero, nameof(value.MaximumTimeout));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value.DefaultTimeout, value.MaximumTimeout, nameof(value.DefaultTimeout));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value.MaximumQueryCharacters, nameof(value.MaximumQueryCharacters));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value.MaximumTextCharacters, nameof(value.MaximumTextCharacters));
    }

    private static ExtensionData Status(string status) => new(
        ImmutableDictionary<string, ExtensionValue>.Empty.Add(
            "agentkit.language.status",
            new ExtensionValue([.. JsonSerializer.SerializeToUtf8Bytes(status)])));

    private static ToolInvocationResult Failure(
        string reason,
        string status,
        ImmutableArray<ContentPart> content) => new(
            new ToolCallOutcome(ToolCallOutcomeKind.Failed, reason, Status(status)),
            content);

    private readonly record struct ParsedArguments(
        LanguageQueryKind Kind,
        FileSystemPath? Path,
        LanguagePosition? Position,
        string? Query,
        int MaximumResults,
        TimeSpan Timeout);

}
