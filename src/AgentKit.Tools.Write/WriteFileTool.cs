// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Write;

using System.Text;

using AgentKit.Tools;

/// <summary>Writes a text file through a keyed <see cref="IFileWriter"/> with explicit dispositions.</summary>
public sealed class WriteFileTool: IToolInvoker, ITool
{
    /// <summary>The stable identity this tool registers under.</summary>
    public static readonly ToolId Id = new("write_file");

    private static readonly JsonElement _inputSchema = JsonDocument.Parse(
        """
        {
          "type": "object",
          "properties": {
            "path": { "type": "string", "description": "The file path, relative to the sandboxed working directory." },
            "content": { "type": "string", "description": "The text content to write." },
            "mode": {
              "type": "string",
              "enum": ["create_or_replace", "create_only", "replace_existing", "append", "overwrite", "create_new"],
              "description": "Required target-state disposition. 'overwrite' and 'create_new' are legacy aliases."
            }
          },
          "required": ["path", "content", "mode"]
        }
        """).RootElement;

    private readonly IFileSystemSelector _fileSystemSelector;
    private readonly IFilePathNormalizer _pathNormalizer;
    private readonly ISecurityAuthoritySelector _authoritySelector;
    private readonly IIdentifierGenerator<SecurityRequestId> _requestIds;
    private readonly TimeProvider _timeProvider;
    private readonly WriteFileToolOptions _options;

    /// <summary>Initializes a new instance of the <see cref="WriteFileTool"/> class.</summary>
    /// <param name="fileSystemSelector">Selects the keyed writer profile.</param>
    /// <param name="pathNormalizer">Normalizes model-supplied paths before authorization.</param>
    /// <param name="authoritySelector">The security authority selector.</param>
    /// <param name="requestIds">The security-request identity generator.</param>
    /// <param name="timeProvider">The deterministic clock used to bound authorization.</param>
    /// <param name="options">The validated options captured at construction.</param>
    /// <exception cref="ArgumentNullException">Any dependency is null.</exception>
    /// <exception cref="ArgumentException"><see cref="WriteFileToolOptions.HostRootPath"/> is not configured.</exception>
    public WriteFileTool(
        IFileSystemSelector fileSystemSelector,
        IFilePathNormalizer pathNormalizer,
        ISecurityAuthoritySelector authoritySelector,
        IIdentifierGenerator<SecurityRequestId> requestIds,
        TimeProvider timeProvider,
        IOptions<WriteFileToolOptions> options)
    {
        ArgumentNullException.ThrowIfNull(fileSystemSelector);
        ArgumentNullException.ThrowIfNull(pathNormalizer);
        ArgumentNullException.ThrowIfNull(authoritySelector);
        ArgumentNullException.ThrowIfNull(requestIds);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Value.HostRootPath);
        _fileSystemSelector = fileSystemSelector;
        _pathNormalizer = pathNormalizer;
        _authoritySelector = authoritySelector;
        _requestIds = requestIds;
        _timeProvider = timeProvider;
        _options = options.Value;
    }

    /// <summary>Gets the immutable descriptor shared with registration and presentation formatting.</summary>
    public static ToolDescriptor Descriptor { get; } = new(
        Id,
        new ToolVersion("1.0"),
        "write_file",
        "Writes a text file within the sandboxed working directory using an explicit create, replace, create-or-replace, or append disposition.",
        new JsonSchema(new JsonSchemaDialectId("https://json-schema.org/draft/2020-12/schema"), _inputSchema),
        outputSchema: null,
        new ToolEffects(ToolEffect.Mutating, idempotency: null, requiredResourceKinds: null),
        new ToolExecutionHints(ToolSchedulingMode.Unspecified, concurrencyKey: null, expectedDuration: null, approvalMayBeCached: null),
        new ToolSourceId("agentkit.tools.write"),
        ExtensionData.Empty);

    internal static ToolDescriptor PresentationDescriptor => Descriptor;

    /// <summary>Gets the default toolset publication selecting this tool from the application tool source.</summary>
    public static ToolsetPublication DefaultToolset { get; } = new(
        new ToolsetKey("agentkit.tools.write"),
        new ToolsetVersion(1),
        new ToolExecutionPolicyReference(new ToolExecutionPolicyKey("standard"), new ToolExecutionPolicyVersion(1)),
        [new ToolsetSourceSelection(ApplicationToolSources.Default)],
        [new ToolAliasAssignment(new ToolAlias("write_file"), new ToolIdentity(Id, Descriptor.Version))]);

    /// <inheritdoc/>
    ToolDescriptor ITool.Descriptor => Descriptor;

    /// <inheritdoc/>
    public ValueTask<ToolInvocationResult> InvokeAsync(
        ToolInvocationContext context,
        CancellationToken cancellationToken = default) =>
        InvokeCoreAsync(ToExecutionContext(context), context.Arguments, cancellationToken);

    /// <inheritdoc/>
    [Obsolete("Legacy host surface.")]

    public Task<ToolInvocationResult> InvokeAsync(ToolInvocationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return InvokeCoreAsync(request.Context, request.Arguments, cancellationToken).AsTask();
    }

    private static ToolExecutionContext ToExecutionContext(ToolInvocationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var authorization = context.InvocationGrant.Authorization
            ?? throw new InvalidOperationException("Tool invocations require grants that retain complete authorization evidence.");
        return new ToolExecutionContext(
            context.AgentId,
            context.SessionId,
            context.CallId,
            context.InvocationGrant.Scope.Correlation,
            context.InvocationGrant.Identity,
            authorization,
            sessionProfile: null);
    }

    private async ValueTask<ToolInvocationResult> InvokeCoreAsync(
        ToolExecutionContext context,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        if (!ToolArguments.TryGetRequiredString(arguments, "path", out var pathText, out var pathError))
        {
            return Failed(pathError, ToolTerminalStatus.InvalidArguments, SideEffectCertainty.DefinitelyNotPerformed);
        }

        if (!arguments.TryGetProperty("content", out var contentProperty)
            || contentProperty.ValueKind != JsonValueKind.String)
        {
            return Failed("A string property 'content' is required.", ToolTerminalStatus.InvalidArguments, SideEffectCertainty.DefinitelyNotPerformed);
        }

        var content = contentProperty.GetString()!;

        if (!TryParseDisposition(arguments, out var disposition, out var modeError))
        {
            return Failed(modeError, ToolTerminalStatus.InvalidArguments, SideEffectCertainty.DefinitelyNotPerformed);
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
        var payload = Encoding.UTF8.GetBytes(content);
        var payloadFingerprint = FileSecurityBinding.ContentFingerprint(payload);

        var authorization = context.Authorization;
        var activated = await _authoritySelector.SelectAsync(authorization, cancellationToken).ConfigureAwait(false);
        if (activated is not SecurityAuthoritySelected selected || selected.Authorization != authorization)
        {
            return Failed("The captured security authority is unavailable.", ToolTerminalStatus.Denied, SideEffectCertainty.DefinitelyNotPerformed);
        }

        var securityRequest = new SecurityRequest(
            _requestIds.Create(),
            authorization.Scope,
            context.ToolCallId,
            authorization.Identity,
            authorization,
            _options.SecurityAudience,
            SecurityOperationKind.FileWrite,
            FileSecurityBinding.WriteEffect(disposition),
            [FileSecurityBinding.Resource(target)],
            FileSecurityBinding.WriteFingerprint(
                target,
                disposition,
                expectedTargetFingerprint: null,
                payload.Length,
                payloadFingerprint,
                FileWriteAtomicityMode.Required,
                FileWriteEffectClass.WorkspaceBytes,
                payloadFingerprint),
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
            FileSystemCapability.Write,
            cancellationToken).ConfigureAwait(false);
        if (selection is not FileSystemWriterSelected writerSelected)
        {
            return Failed("The configured file-system writer profile is unavailable.", ToolTerminalStatus.Denied, SideEffectCertainty.DefinitelyNotPerformed);
        }

        var operation = new AuthorizedFileWrite(
            resolved,
            disposition,
            expectedTargetFingerprint: null,
            payload.Length,
            payloadFingerprint,
            FileWriteAtomicityMode.Required,
            FileWriteEffectClass.WorkspaceBytes,
            allowed.Grant);
        var writeContent = new FileWriteContent(payload, payloadFingerprint);
        var result = await writerSelected.Writer.WriteAsync(operation, writeContent, cancellationToken).ConfigureAwait(false);

        return result switch
        {
            FileWriteSuccess success => Success($"Wrote {success.PayloadBytes} byte(s) to '{pathText}'."),
            FileWriteConflict => Failed($"A file already exists at '{pathText}'.", ToolTerminalStatus.InvocationFailed, SideEffectCertainty.DefinitelyNotPerformed),
            FileWriteNotFound => Failed($"No file exists at '{pathText}'.", ToolTerminalStatus.InvocationFailed, SideEffectCertainty.DefinitelyNotPerformed),
            FileWriteDenied denied => Failed(denied.SafeMessage, ToolTerminalStatus.Denied, SideEffectCertainty.DefinitelyNotPerformed),
            FileWriteFailed failed => Failed(failed.SafeMessage, ToolTerminalStatus.InvocationFailed, SideEffectCertainty.Unknown),
            FileWriteLimitExceeded => Failed("The write exceeded configured bounds.", ToolTerminalStatus.InvocationFailed, SideEffectCertainty.DefinitelyNotPerformed),
            FileWriteCancelled => Failed("The write was cancelled.", ToolTerminalStatus.Cancelled, SideEffectCertainty.DefinitelyNotPerformed),
            FileWriteUnsupported => Failed("The configured writer does not support this disposition.", ToolTerminalStatus.Unsupported, SideEffectCertainty.DefinitelyNotPerformed),
            _ => Failed("The file writer returned an unrecognized outcome.", ToolTerminalStatus.ProtocolFailed, SideEffectCertainty.Unknown),
        };
    }

    private static bool TryParseDisposition(
        JsonElement arguments,
        out FileWriteDisposition disposition,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(false)] out string? error)
    {
        if (!ToolArguments.TryGetRequiredString(arguments, "mode", out var text, out error))
        {
            disposition = default;
            return false;
        }

        switch (text)
        {
            case "create_or_replace":
            case "overwrite":
                disposition = FileWriteDisposition.CreateOrReplace;
                error = null;
                return true;
            case "create_only":
            case "create_new":
                disposition = FileWriteDisposition.CreateOnly;
                error = null;
                return true;
            case "replace_existing":
                disposition = FileWriteDisposition.ReplaceExisting;
                error = null;
                return true;
            case "append":
                disposition = FileWriteDisposition.Append;
                error = null;
                return true;
            default:
                disposition = default;
                error = $"'mode' must be one of 'create_or_replace', 'create_only', 'replace_existing', or 'append'; got '{text}'.";
                return false;
        }
    }

    private static ToolInvocationResult Success(string message) => new(
        new ToolCallOutcome(ToolCallOutcomeKind.Success, ToolTerminalStatus.Succeeded, SideEffectCertainty.DefinitelyPerformed, false, null, ExtensionData.Empty),
        [new TextPart(message, TextSemantics.Plain, ExtensionData.Empty)]);

    private static ToolInvocationResult Failed(string reason, ToolTerminalStatus sourceStatus, SideEffectCertainty certainty) => new(
        new ToolCallOutcome(sourceStatus.ToOutcomeKind(), sourceStatus, certainty, false, reason, ExtensionData.Empty),
        []);
}
