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

    /// <summary>Initializes a new instance of the <see cref="WriteFileTool"/> class.</summary>
    /// <param name="fileSystem">The file system this tool writes through.</param>
    /// <exception cref="ArgumentNullException"><paramref name="fileSystem"/> is null.</exception>
    public WriteFileTool(IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        _fileSystem = fileSystem;
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

        var result = await _fileSystem.WriteAsync(new FileWriteRequest(path, content, mode), cancellationToken).ConfigureAwait(false);

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
