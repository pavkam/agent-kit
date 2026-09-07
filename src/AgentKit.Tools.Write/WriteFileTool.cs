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
              "enum": ["overwrite", "create_new", "append"],
              "description": "How to treat an existing file. Defaults to 'overwrite'."
            }
          },
          "required": ["path", "content"]
        }
        """).RootElement;

    private readonly IFileSystem _fileSystem;
    private readonly ISecurityAuthority _securityAuthority;
    private readonly IIdentifierGenerator<SecurityRequestId> _requestIds;
    private readonly TimeProvider _timeProvider;

    /// <summary>Initializes a new instance of the <see cref="WriteFileTool"/> class.</summary>
    /// <param name="fileSystem">The file system this tool writes through.</param>
    /// <param name="securityAuthority">The system-wide authority used after path, content, and disposition normalization.</param>
    /// <param name="requestIds">The security-request identity generator.</param>
    /// <param name="timeProvider">The deterministic clock used to bound authorization.</param>
    /// <exception cref="ArgumentNullException">Any dependency is null.</exception>
    public WriteFileTool(
        IFileSystem fileSystem,
        ISecurityAuthority securityAuthority,
        IIdentifierGenerator<SecurityRequestId> requestIds,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(securityAuthority);
        ArgumentNullException.ThrowIfNull(requestIds);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _fileSystem = fileSystem;
        _securityAuthority = securityAuthority;
        _requestIds = requestIds;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc/>
    public ToolDescriptor Descriptor { get; } = new(
        Id,
        null,
        "write_file",
        "Writes a text file within the sandboxed working directory, creating, overwriting, or appending as requested.",
        _inputSchema,
        ToolEffect.Mutating,
        ExtensionData.Empty);

    /// <inheritdoc/>
    public async Task<ToolInvocationResult> InvokeAsync(ToolInvocationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!ToolArguments.TryGetRequiredString(request.Arguments, "path", out var pathText, out var pathError))
        {
            return Failed(pathError);
        }

        if (!ToolArguments.TryGetRequiredString(request.Arguments, "content", out var content, out var contentError))
        {
            return Failed(contentError);
        }

        if (!TryParseMode(request.Arguments, out var mode, out var modeError))
        {
            return Failed(modeError);
        }

        FileSystemPath path;
        try
        {
            path = new FileSystemPath(pathText);
        }
        catch (ArgumentException ex)
        {
            return Failed($"Invalid path: {ex.Message}");
        }

        var context = request.Context;
        var securityRequest = new SecurityRequest(
            _requestIds.Create(),
            new SecurityAuthorizationScope(context.AgentId, context.SessionId, context.Correlation),
            context.ToolCallId,
            context.Identity,
            _fileSystem.SecurityAudience,
            SecurityOperationKind.FileWrite,
            FileSecurityBinding.WriteEffect(mode),
            [FileSecurityBinding.Resource(path)],
            FileSecurityBinding.WriteFingerprint(path, content, mode),
            _timeProvider.GetUtcNow().AddMinutes(1));
        var decision = await _securityAuthority.AuthorizeAsync(securityRequest, cancellationToken).ConfigureAwait(false);
        if (decision is SecurityDenied authorizationDenied)
        {
            return Failed(authorizationDenied.Denial.SafeMessage);
        }

        if (decision is not SecurityAllowed allowed)
        {
            return Failed("The security authority returned an unsupported decision.");
        }

        var result = await _fileSystem.WriteAsync(
            new FileWriteRequest(path, content, mode, allowed.Grant), cancellationToken).ConfigureAwait(false);

        return result switch
        {
            FileWritten written => Success($"Wrote {written.BytesWritten} byte(s) to '{pathText}'."),
            FileAlreadyExists => Failed($"A file already exists at '{pathText}'."),
            FileWriteDenied denied => Failed(denied.SafeMessage),
            FileWriteFailed failed => Failed(failed.SafeMessage),
            _ => Failed("The file system returned an unrecognized outcome.")
        };
    }

    private static bool TryParseMode(JsonElement arguments, out FileWriteMode mode, [System.Diagnostics.CodeAnalysis.NotNullWhen(false)] out string? error)
    {
        if (!ToolArguments.TryGetOptionalString(arguments, "mode", out var text) || text is null)
        {
            mode = FileWriteMode.CreateOrOverwrite;
            error = null;
            return true;
        }

        switch (text)
        {
            case "overwrite":
                mode = FileWriteMode.CreateOrOverwrite;
                error = null;
                return true;
            case "create_new":
                mode = FileWriteMode.CreateNew;
                error = null;
                return true;
            case "append":
                mode = FileWriteMode.Append;
                error = null;
                return true;
            default:
                mode = default;
                error = $"'mode' must be one of 'overwrite', 'create_new', or 'append'; got '{text}'.";
                return false;
        }
    }

    private static ToolInvocationResult Success(string message) => new(
        new ToolCallOutcome(ToolCallOutcomeKind.Success, null, ExtensionData.Empty),
        [new TextPart(message, TextSemantics.Plain, ExtensionData.Empty)]);

    private static ToolInvocationResult Failed(string reason) => new(
        new ToolCallOutcome(ToolCallOutcomeKind.Failed, reason, ExtensionData.Empty),
        []);
}
