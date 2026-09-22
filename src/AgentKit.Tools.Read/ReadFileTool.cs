// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Read;

using System.Text;

using AgentKit.Tools;

/// <summary>Reads a text file through a keyed <see cref="IFileReader"/> with optional line-range selection.</summary>
public sealed class ReadFileTool: IToolInvoker, ITool
{
    /// <summary>The stable identity this tool registers under.</summary>
    public static readonly ToolId Id = new("read_file");

    /// <summary>Outcome extension key reporting whether the window reached the end of the file.</summary>
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
    private readonly IFileSystemSelector _fileSystemSelector;
    private readonly IFilePathNormalizer _pathNormalizer;
    private readonly ISecurityAuthoritySelector _authoritySelector;
    private readonly IIdentifierGenerator<SecurityRequestId> _requestIds;
    private readonly IIdentifierGenerator<FileOperationId> _fileOperationIds;
    private readonly TimeProvider _timeProvider;
    private readonly ReadFileToolOptions _options;

    /// <summary>Initializes a new instance of the <see cref="ReadFileTool"/> class.</summary>
    /// <param name="fileSystemSelector">Selects the keyed reader profile.</param>
    /// <param name="pathNormalizer">Normalizes model-supplied paths before authorization.</param>
    /// <param name="authoritySelector">The security authority selector.</param>
    /// <param name="requestIds">The security-request identity generator.</param>
    /// <param name="fileOperationIds">The file-operation identity generator.</param>
    /// <param name="timeProvider">The deterministic clock used to bound authorization.</param>
    /// <param name="options">The validated options captured at construction.</param>
    /// <exception cref="ArgumentNullException">Any dependency is null.</exception>
    /// <exception cref="ArgumentException"><see cref="ReadFileToolOptions.HostRootPath"/> is not configured.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A configured line bound is invalid.</exception>
    public ReadFileTool(
        IFileSystemSelector fileSystemSelector,
        IFilePathNormalizer pathNormalizer,
        ISecurityAuthoritySelector authoritySelector,
        IIdentifierGenerator<SecurityRequestId> requestIds,
        IIdentifierGenerator<FileOperationId> fileOperationIds,
        TimeProvider timeProvider,
        IOptions<ReadFileToolOptions> options)
    {
        ArgumentNullException.ThrowIfNull(fileSystemSelector);
        ArgumentNullException.ThrowIfNull(pathNormalizer);
        ArgumentNullException.ThrowIfNull(authoritySelector);
        ArgumentNullException.ThrowIfNull(requestIds);
        ArgumentNullException.ThrowIfNull(fileOperationIds);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Value.HostRootPath);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.Value.DefaultMaximumLines);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.Value.MaximumLines);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            options.Value.DefaultMaximumLines, options.Value.MaximumLines);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.Value.MaximumReadBytes);
        _fileSystemSelector = fileSystemSelector;
        _pathNormalizer = pathNormalizer;
        _authoritySelector = authoritySelector;
        _requestIds = requestIds;
        _fileOperationIds = fileOperationIds;
        _timeProvider = timeProvider;
        _options = options.Value;
    }

    /// <summary>Gets the immutable descriptor shared with registration and presentation formatting.</summary>
    public static ToolDescriptor Descriptor { get; } = new(
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

    /// <summary>Gets the immutable descriptor shared with exact presentation formatting.</summary>
    internal static ToolDescriptor PresentationDescriptor => Descriptor;

    /// <summary>Gets the default toolset publication selecting this tool from the application tool source.</summary>
    public static ToolsetPublication DefaultToolset { get; } = new(
        new ToolsetKey("agentkit.tools.read"),
        new ToolsetVersion(1),
        new ToolExecutionPolicyReference(new ToolExecutionPolicyKey("standard"), new ToolExecutionPolicyVersion(1)),
        [new ToolsetSourceSelection(ApplicationToolSources.Default)],
        [new ToolAliasAssignment(new ToolAlias("read_file"), new ToolIdentity(Id, Descriptor.Version))]);

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
        return InvokeCoreAsync(authorization, context.AgentId, context.CallId, context.Arguments, cancellationToken);
    }

    /// <inheritdoc/>
    [Obsolete("Legacy host surface.")]

    public Task<ToolInvocationResult> InvokeAsync(ToolInvocationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return InvokeCoreAsync(
            request.Context.Authorization,
            request.Context.AgentId,
            request.Context.ToolCallId,
            request.Arguments,
            cancellationToken).AsTask();
    }

    private async ValueTask<ToolInvocationResult> InvokeCoreAsync(
        SecurityAuthorizationContext authorization,
        AgentId agentId,
        ToolCallId callId,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        if (!ToolArguments.TryGetRequiredString(arguments, "path", out var pathText, out var pathError))
        {
            return Failed(pathError, ToolTerminalStatus.InvalidArguments, SideEffectCertainty.DefinitelyNotPerformed);
        }

        if (!ToolArguments.TryGetOptionalInt(arguments, "offset", out var offset, out var offsetError))
        {
            return Failed(offsetError, ToolTerminalStatus.InvalidArguments, SideEffectCertainty.DefinitelyNotPerformed);
        }

        if (!ToolArguments.TryGetOptionalInt(arguments, "limit", out var limit, out var limitError))
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

        var normalized = _pathNormalizer.Normalize(new FilePathInput(_options.RootId, pathText), _options.PathPolicy);
        if (normalized is not FilePathNormalizationSuccess normalizedPath)
        {
            var message = normalized is FilePathNormalizationFailed failed
                ? failed.SafeMessage
                : "The path could not be normalized.";
            return Failed(message, ToolTerminalStatus.InvalidArguments, SideEffectCertainty.DefinitelyNotPerformed);
        }

        var target = FileHostTargetBinding.Target(_options.RootId, normalizedPath.Path);
        var resolved = FileHostTargetBinding.Resolve(_options.RootId, normalizedPath.Path, _options.HostRootPath);

        var activated = await _authoritySelector.SelectAsync(authorization, cancellationToken).ConfigureAwait(false);
        if (activated is not SecurityAuthoritySelected selected || selected.Authorization != authorization)
        {
            return Failed("The captured security authority is unavailable.", ToolTerminalStatus.Denied, SideEffectCertainty.DefinitelyNotPerformed);
        }

        var runId = authorization.Scope.Correlation is InRunOperationCorrelation inRun
            ? (RunId?) inRun.RunId
            : null;
        var readRequest = new FileReadRequest(
            _fileOperationIds.Create(),
            authorization.Scope.Correlation.OperationId,
            agentId,
            runId,
            target,
            new FileReadBounds(_options.MaximumReadBytes));

        var securityRequest = new SecurityRequest(
            _requestIds.Create(),
            authorization.Scope,
            callId,
            authorization.Identity,
            authorization,
            _options.SecurityAudience,
            SecurityOperationKind.FileRead,
            SecurityEffect.Observe,
            [FileSecurityBinding.Resource(target)],
            FileSecurityBinding.ReadFingerprint(readRequest),
            _timeProvider.GetUtcNow().AddMinutes(1));
        var decision = await selected.Authority.AuthorizeAsync(securityRequest, hooks: null, cancellationToken).ConfigureAwait(false);
        if (decision is SecurityDenied authorizationDenied)
        {
            return Failed(authorizationDenied.Denial.SafeMessage, ToolTerminalStatus.Denied, SideEffectCertainty.DefinitelyNotPerformed);
        }

        if (decision is not SecurityAllowed allowed)
        {
            return Failed("The security authority returned an unsupported decision.", ToolTerminalStatus.Unsupported, SideEffectCertainty.DefinitelyNotPerformed);
        }

        var selection = await _fileSystemSelector.SelectAsync(
            _options.ProfileKey,
            FileSystemCapability.Read,
            cancellationToken).ConfigureAwait(false);
        if (selection is not FileSystemReaderSelected readerSelected)
        {
            return Failed("The configured file-system reader profile is unavailable.", ToolTerminalStatus.Denied, SideEffectCertainty.DefinitelyNotPerformed);
        }

        var operation = new AuthorizedFileRead(readRequest, resolved, allowed.Grant);
        var openResult = await readerSelected.Reader.OpenReadAsync(operation, cancellationToken).ConfigureAwait(false);
        return openResult switch
        {
            FileReadHandleOpened opened => await ReadHandleAsync(opened, pathText, offset, limit ?? _options.DefaultMaximumLines, cancellationToken).ConfigureAwait(false),
            FileReadOpenNotFound => Failed($"No file exists at '{pathText}'.", ToolTerminalStatus.InvocationFailed, SideEffectCertainty.DefinitelyNotPerformed),
            FileReadOpenDenied denied => Failed(denied.SafeMessage, ToolTerminalStatus.Denied, SideEffectCertainty.DefinitelyNotPerformed),
            FileReadOpenFailed failed => Failed(failed.SafeMessage, ToolTerminalStatus.InvocationFailed, SideEffectCertainty.Unknown),
            FileReadOpenCancelled => Failed("The read was cancelled.", ToolTerminalStatus.Cancelled, SideEffectCertainty.DefinitelyNotPerformed),
            _ => Failed("The file reader returned an unrecognized outcome.", ToolTerminalStatus.ProtocolFailed, SideEffectCertainty.Unknown),
        };
    }

    private static async Task<ToolInvocationResult> ReadHandleAsync(
        FileReadHandleOpened opened,
        string pathText,
        int? offset,
        int limit,
        CancellationToken cancellationToken)
    {
        await using var handle = opened.Handle;
        string content;
        try
        {
            using var reader = new StreamReader(handle.Content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
            content = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DecoderFallbackException)
        {
            return Failed($"The file at '{pathText}' is not valid UTF-8 text.", ToolTerminalStatus.InvocationFailed, SideEffectCertainty.DefinitelyNotPerformed);
        }

        return Success(ApplyRange(content, offset, limit));
    }

    private static (string Text, bool Complete) ApplyRange(string content, int? offset, int limit)
    {
        Debug.Assert(offset is null or >= 1, "Offset validation must precede range application.");
        Debug.Assert(limit >= 1, "The effective limit must be a positive line count.");

        var normalized = content.ReplaceLineEndings("\n");
        var lines = normalized.Split('\n');
        if (normalized.Length > 0 && normalized[^1] == '\n')
        {
            lines = lines[..^1];
        }

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
