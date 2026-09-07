// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.FileSystem;

/// <summary>
/// A tool that reads a text file through an injected <see cref="IFileSystem"/>,
/// with optional 1-indexed line-range selection.
/// </summary>
/// <remarks>
/// This tool never touches the host file system directly: every read goes
/// through the injected <see cref="IFileSystem"/>, which re-validates the
/// requested path against its own configured boundary regardless of
/// whatever authorization already let the call reach this point.
/// </remarks>
public sealed class ReadFileTool: ITool
{
    /// <summary>The stable identity this tool registers under.</summary>
    public static readonly ToolId Id = new("read_file");

    private static readonly JsonElement _inputSchema = JsonDocument.Parse(
        """
        {
          "type": "object",
          "properties": {
            "path": { "type": "string", "description": "The file path, relative to the sandboxed working directory." },
            "offset": { "type": "integer", "minimum": 1, "description": "The 1-indexed line number to start reading from." },
            "limit": { "type": "integer", "minimum": 1, "description": "The maximum number of lines to return." }
          },
          "required": ["path"]
        }
        """).RootElement;

    private readonly IFileSystem _fileSystem;

    /// <summary>Initializes a new instance of the <see cref="ReadFileTool"/> class.</summary>
    /// <param name="fileSystem">The file system this tool reads through.</param>
    /// <exception cref="ArgumentNullException"><paramref name="fileSystem"/> is null.</exception>
    public ReadFileTool(IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        _fileSystem = fileSystem;
    }

    /// <inheritdoc/>
    public ToolDescriptor Descriptor { get; } = new(
        Id,
        null,
        "read_file",
        "Reads a text file within the sandboxed working directory, optionally limited to a line range.",
        _inputSchema,
        ToolEffect.ReadOnly,
        ExtensionData.Empty);

    /// <inheritdoc/>
    public async Task<ToolInvocationResult> InvokeAsync(ToolInvocationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!ToolArguments.TryGetRequiredString(request.Arguments, "path", out var pathText, out var pathError))
        {
            return Failed(pathError);
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

        if (!ToolArguments.TryGetOptionalInt(request.Arguments, "offset", out var offset, out var offsetError))
        {
            return Failed(offsetError);
        }

        if (!ToolArguments.TryGetOptionalInt(request.Arguments, "limit", out var limit, out var limitError))
        {
            return Failed(limitError);
        }

        if (offset is < 1 || limit is < 1)
        {
            return Failed("'offset' and 'limit' must be positive integers when provided.");
        }

        var result = await _fileSystem.ReadAsync(new FileReadRequest(path), cancellationToken).ConfigureAwait(false);

        return result switch
        {
            FileRead read => Success(ApplyRange(read.Content, offset, limit)),
            FileNotFound => Failed($"No file exists at '{pathText}'."),
            FileReadDenied denied => Failed(denied.SafeMessage),
            FileReadFailed failed => Failed(failed.SafeMessage),
            _ => Failed("The file system returned an unrecognized outcome.")
        };
    }

    private static string ApplyRange(string content, int? offset, int? limit)
    {
        if (offset is null && limit is null)
        {
            return content;
        }

        var lines = content.ReplaceLineEndings("\n").Split('\n');
        var startIndex = Math.Min(Math.Max((offset ?? 1) - 1, 0), lines.Length);
        var count = Math.Min(limit ?? int.MaxValue, lines.Length - startIndex);

        return string.Join('\n', lines.Skip(startIndex).Take(Math.Max(count, 0)));
    }

    private static ToolInvocationResult Success(string content) => new(
        new ToolCallOutcome(ToolCallOutcomeKind.Success, null, ExtensionData.Empty),
        [new TextPart(content, TextSemantics.Plain, ExtensionData.Empty)]);

    private static ToolInvocationResult Failed(string reason) => new(
        new ToolCallOutcome(ToolCallOutcomeKind.Failed, reason, ExtensionData.Empty),
        []);
}
