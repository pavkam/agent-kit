// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Read;

/// <summary>
/// A tool that reads a text file through an injected <see cref="IFileSystem"/>,
/// with optional 1-indexed line-range selection.
/// </summary>
/// <remarks>
/// <para>
/// This tool never touches the host file system directly: every read goes
/// through the injected <see cref="IFileSystem"/>, which re-validates the
/// requested path against its own configured boundary regardless of
/// whatever authorization already let the call reach this point.
/// </para>
/// <para>
/// The returned window is always finite. An omitted <c>limit</c> uses
/// <see cref="ReadFileToolOptions.DefaultMaximumLines"/>, and an explicit
/// <c>limit</c> above <see cref="ReadFileToolOptions.MaximumLines"/> is
/// rejected before authorization. A successful outcome records whether the
/// window reached the last logical line under the
/// <see cref="CompleteExtensionKey"/> extension so callers can tell a
/// clipped read from a complete one without parsing the text.
/// </para>
/// </remarks>
public sealed class ReadFileTool: ITool
{
    /// <summary>The stable identity this tool registers under.</summary>
    public static readonly ToolId Id = new("read_file");

    /// <summary>
    /// The outcome extension key whose canonical JSON boolean reports whether the returned
    /// window reached the end of the file (<c>true</c>) or more lines remain (<c>false</c>).
    /// </summary>
    public const string CompleteExtensionKey = "agentkit.read.complete";

    private static readonly JsonElement _inputSchema = JsonDocument.Parse(
        """
        {
          "type": "object",
          "properties": {
            "path": { "type": "string", "description": "The file path, relative to the sandboxed working directory." },
            "offset": { "type": "integer", "minimum": 1, "description": "The 1-indexed line number to start reading from." },
            "limit": { "type": "integer", "minimum": 1, "description": "The maximum number of lines to return. When omitted, a configured default window applies; values above the configured ceiling are rejected." }
          },
          "required": ["path"]
        }
        """).RootElement;

    private static readonly ExtensionData _completeExtensions = CompleteExtensions(true);
    private static readonly ExtensionData _incompleteExtensions = CompleteExtensions(false);

    private readonly IFileSystem _fileSystem;
    private readonly ISecurityAuthority _securityAuthority;
    private readonly IIdentifierGenerator<SecurityRequestId> _requestIds;
    private readonly TimeProvider _timeProvider;
    private readonly ReadFileToolOptions _options;

    /// <summary>Initializes a new instance of the <see cref="ReadFileTool"/> class.</summary>
    /// <param name="fileSystem">The file system this tool reads through.</param>
    /// <param name="securityAuthority">The system-wide authority used after path and argument normalization.</param>
    /// <param name="requestIds">The security-request identity generator.</param>
    /// <param name="timeProvider">The deterministic clock used to bound authorization.</param>
    /// <param name="options">The validated line-window options; the value is captured once at construction.</param>
    /// <exception cref="ArgumentNullException">Any dependency is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A configured line bound is not positive or <see cref="ReadFileToolOptions.DefaultMaximumLines"/>
    /// exceeds <see cref="ReadFileToolOptions.MaximumLines"/>.
    /// </exception>
    public ReadFileTool(
        IFileSystem fileSystem,
        ISecurityAuthority securityAuthority,
        IIdentifierGenerator<SecurityRequestId> requestIds,
        TimeProvider timeProvider,
        IOptions<ReadFileToolOptions> options)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(securityAuthority);
        ArgumentNullException.ThrowIfNull(requestIds);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.Value.DefaultMaximumLines);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.Value.MaximumLines);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            options.Value.DefaultMaximumLines, options.Value.MaximumLines);
        _fileSystem = fileSystem;
        _securityAuthority = securityAuthority;
        _requestIds = requestIds;
        _timeProvider = timeProvider;
        _options = options.Value;
    }

    /// <summary>Gets the immutable descriptor shared with exact presentation formatting.</summary>
    /// <value>The source-owned identity, schema, effects, and hints for this tool.</value>
    internal static ToolDescriptor PresentationDescriptor { get; } = new(
        Id,
        new ToolVersion("1.0"),
        "read_file",
        "Reads a text file within the sandboxed working directory, optionally limited to a line range.",
        new JsonSchema(new JsonSchemaDialectId("https://json-schema.org/draft/2020-12/schema"), _inputSchema),
        outputSchema: null,
        new ToolEffects(ToolEffect.ReadOnly, idempotency: null, requiredResourceKinds: null),
        new ToolExecutionHints(ToolSchedulingMode.Unspecified, concurrencyKey: null, expectedDuration: null, approvalMayBeCached: null),
        new ToolSourceId("agentkit.tools.read"),
        ExtensionData.Empty);

    /// <inheritdoc/>
    public ToolDescriptor Descriptor => PresentationDescriptor;

    /// <inheritdoc/>
    public async Task<ToolInvocationResult> InvokeAsync(ToolInvocationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!ToolArguments.TryGetRequiredString(request.Arguments, "path", out var pathText, out var pathError))
        {
            return Failed(pathError, ToolTerminalStatus.InvalidArguments, SideEffectCertainty.DefinitelyNotPerformed);
        }

        FileSystemPath path;
        try
        {
            path = new FileSystemPath(pathText);
        }
        catch (ArgumentException ex)
        {
            return Failed($"Invalid path: {ex.Message}", ToolTerminalStatus.InvalidArguments, SideEffectCertainty.DefinitelyNotPerformed);
        }

        if (!ToolArguments.TryGetOptionalInt(request.Arguments, "offset", out var offset, out var offsetError))
        {
            return Failed(offsetError, ToolTerminalStatus.InvalidArguments, SideEffectCertainty.DefinitelyNotPerformed);
        }

        if (!ToolArguments.TryGetOptionalInt(request.Arguments, "limit", out var limit, out var limitError))
        {
            return Failed(limitError, ToolTerminalStatus.InvalidArguments, SideEffectCertainty.DefinitelyNotPerformed);
        }

        if (offset is < 1 || limit is < 1)
        {
            return Failed("'offset' and 'limit' must be positive integers when provided.", ToolTerminalStatus.InvalidArguments, SideEffectCertainty.DefinitelyNotPerformed);
        }

        if (limit > _options.MaximumLines)
        {
            return Failed($"Property 'limit' must be between 1 and {_options.MaximumLines}.", ToolTerminalStatus.InvalidArguments, SideEffectCertainty.DefinitelyNotPerformed);
        }

        var context = request.Context;
        var securityRequest = new SecurityRequest(
            _requestIds.Create(),
            new SecurityAuthorizationScope(context.AgentId, context.SessionId, context.Correlation),
            context.ToolCallId,
            context.Identity,
            _fileSystem.SecurityAudience,
            SecurityOperationKind.FileRead,
            SecurityEffect.Observe,
            [FileSecurityBinding.Resource(path)],
            FileSecurityBinding.ReadFingerprint(path),
            _timeProvider.GetUtcNow().AddMinutes(1));
        var decision = await _securityAuthority.AuthorizeAsync(securityRequest, cancellationToken).ConfigureAwait(false);
        if (decision is SecurityDenied authorizationDenied)
        {
            return Failed(authorizationDenied.Denial.SafeMessage, ToolTerminalStatus.Denied, SideEffectCertainty.DefinitelyNotPerformed);
        }

        if (decision is not SecurityAllowed allowed)
        {
            return Failed("The security authority returned an unsupported decision.", ToolTerminalStatus.Unsupported, SideEffectCertainty.DefinitelyNotPerformed);
        }

        var result = await _fileSystem.ReadAsync(new FileReadRequest(path, allowed.Grant), cancellationToken).ConfigureAwait(false);

        return result switch
        {
            FileRead read => Success(ApplyRange(read.Content, offset, limit ?? _options.DefaultMaximumLines)),
            FileNotFound => Failed($"No file exists at '{pathText}'.", ToolTerminalStatus.InvocationFailed, SideEffectCertainty.DefinitelyNotPerformed),
            FileReadDenied denied => Failed(denied.SafeMessage, ToolTerminalStatus.Denied, SideEffectCertainty.DefinitelyNotPerformed),
            FileReadFailed failed => Failed(failed.SafeMessage, ToolTerminalStatus.InvocationFailed, SideEffectCertainty.Unknown),
            _ => Failed("The file system returned an unrecognized outcome.", ToolTerminalStatus.ProtocolFailed, SideEffectCertainty.Unknown)
        };
    }

    private static (string Text, bool Complete) ApplyRange(string content, int? offset, int limit)
    {
        Debug.Assert(offset is null or >= 1, "Offset validation must precede range application.");
        Debug.Assert(limit >= 1, "The effective limit must be a positive line count.");

        var lines = content.ReplaceLineEndings("\n").Split('\n');
        var startIndex = Math.Min(Math.Max((offset ?? 1) - 1, 0), lines.Length);
        var count = Math.Max(Math.Min(limit, lines.Length - startIndex), 0);
        var complete = startIndex + count >= lines.Length;
        if (offset is null && complete)
        {
            return (content, true);
        }

        return (string.Join('\n', lines.Skip(startIndex).Take(count)), complete);
    }

    private static ExtensionData CompleteExtensions(bool complete) => new(
        ImmutableDictionary<string, ExtensionValue>.Empty.Add(
            CompleteExtensionKey,
            new ExtensionValue([.. JsonSerializer.SerializeToUtf8Bytes(complete)])));

    private static ToolInvocationResult Success((string Text, bool Complete) window) => new(
        new ToolCallOutcome(
            ToolCallOutcomeKind.Success,
            ToolTerminalStatus.Succeeded,
            SideEffectCertainty.DefinitelyPerformed,
            false,
            null,
            window.Complete ? _completeExtensions : _incompleteExtensions),
        [new TextPart(window.Text, TextSemantics.Plain, ExtensionData.Empty)]);

    private static ToolInvocationResult Failed(string reason, ToolTerminalStatus sourceStatus, SideEffectCertainty certainty) => new(
        new ToolCallOutcome(sourceStatus.ToOutcomeKind(), sourceStatus, certainty, false, reason, ExtensionData.Empty),
        []);
}
