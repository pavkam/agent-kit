// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Write;

/// <summary>
/// A tool that writes a text file through an injected <see cref="IFileSystem"/>.
/// </summary>
/// <remarks>
/// This tool never touches the host file system directly: every write goes
/// through the injected <see cref="IFileSystem"/>, which re-validates the
/// requested path and content size against its own configured boundary
/// regardless of whatever authorization already let the call reach this
/// point.
/// </remarks>
public sealed class WriteFileTool: ITool
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
    [Obsolete("Use IFileWriter after WS5-C7 migrates write_file.")]
    private readonly IFileSystem _fileSystem;
    private readonly ISecurityAuthoritySelector _authoritySelector;
    private readonly IIdentifierGenerator<SecurityRequestId> _requestIds;
    private readonly TimeProvider _timeProvider;

    /// <summary>Initializes a new instance of the <see cref="WriteFileTool"/> class.</summary>
    /// <param name="fileSystem">The file system this tool writes through.</param>
    /// <param name="authoritySelector">The security authority selector used after path, content, and disposition normalization.</param>
    /// <param name="requestIds">The security-request identity generator.</param>
    /// <param name="timeProvider">The deterministic clock used to bound authorization.</param>
    /// <exception cref="ArgumentNullException">Any dependency is null.</exception>
    [Obsolete("Use IFileWriter after WS5-C7 migrates write_file.")]
    public WriteFileTool(
        IFileSystem fileSystem,
        ISecurityAuthoritySelector authoritySelector,
        IIdentifierGenerator<SecurityRequestId> requestIds,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(authoritySelector);
        ArgumentNullException.ThrowIfNull(requestIds);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _fileSystem = fileSystem;
        _authoritySelector = authoritySelector;
        _requestIds = requestIds;
        _timeProvider = timeProvider;
    }

    /// <summary>Gets the immutable descriptor shared with exact presentation formatting.</summary>
    /// <value>The source-owned identity, schema, effects, and hints for this tool.</value>
    internal static ToolDescriptor PresentationDescriptor { get; } = new(
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

    /// <inheritdoc/>
    public ToolDescriptor Descriptor => PresentationDescriptor;

    /// <inheritdoc/>
    [Obsolete("Use IFileWriter after WS5-C7 migrates write_file.")]
    public async Task<ToolInvocationResult> InvokeAsync(ToolInvocationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!ToolArguments.TryGetRequiredString(request.Arguments, "path", out var pathText, out var pathError))
        {
            return Failed(pathError, ToolTerminalStatus.InvalidArguments, SideEffectCertainty.DefinitelyNotPerformed);
        }

        if (!request.Arguments.TryGetProperty("content", out var contentProperty)
            || contentProperty.ValueKind != JsonValueKind.String)
        {
            return Failed("A string property 'content' is required.", ToolTerminalStatus.InvalidArguments, SideEffectCertainty.DefinitelyNotPerformed);
        }
        var content = contentProperty.GetString()!;

        if (!TryParseMode(request.Arguments, out var mode, out var modeError))
        {
            return Failed(modeError, ToolTerminalStatus.InvalidArguments, SideEffectCertainty.DefinitelyNotPerformed);
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

        var context = request.Context;
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
            _fileSystem.SecurityAudience,
            SecurityOperationKind.FileWrite,
            FileSecurityBinding.WriteEffect(mode),
            [FileSecurityBinding.Resource(path)],
            FileSecurityBinding.WriteFingerprint(path, content, mode),
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

        var result = await _fileSystem.WriteAsync(
            new FileWriteRequest(path, content, mode, allowed.Grant), cancellationToken).ConfigureAwait(false);

        return result switch
        {
            LegacyFileWritten written => Success($"Wrote {written.BytesWritten} byte(s) to '{pathText}'."),
            LegacyFileAlreadyExists => Failed($"A file already exists at '{pathText}'.", ToolTerminalStatus.InvocationFailed, SideEffectCertainty.DefinitelyNotPerformed),
            LegacyFileWriteDenied denied => Failed(denied.SafeMessage, ToolTerminalStatus.Denied, SideEffectCertainty.DefinitelyNotPerformed),
            LegacyFileWriteFailed failed => Failed(failed.SafeMessage, ToolTerminalStatus.InvocationFailed, SideEffectCertainty.Unknown),
            _ => Failed("The file system returned an unrecognized outcome.", ToolTerminalStatus.ProtocolFailed, SideEffectCertainty.Unknown)
        };
    }

    private static bool TryParseMode(JsonElement arguments, out FileWriteMode mode, [System.Diagnostics.CodeAnalysis.NotNullWhen(false)] out string? error)
    {
        if (!ToolArguments.TryGetRequiredString(arguments, "mode", out var text, out error))
        {
            mode = default;
            return false;
        }

        switch (text)
        {
            case "create_or_replace":
            case "overwrite":
                mode = FileWriteMode.CreateOrOverwrite;
                error = null;
                return true;
            case "create_only":
            case "create_new":
                mode = FileWriteMode.CreateNew;
                error = null;
                return true;
            case "replace_existing":
                mode = FileWriteMode.ReplaceExisting;
                error = null;
                return true;
            case "append":
                mode = FileWriteMode.Append;
                error = null;
                return true;
            default:
                mode = default;
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
