// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Host.FileSystem;

using Microsoft.Extensions.Options;

/// <summary>
/// The default <see cref="IFileSystem"/>: resolves every
/// <see cref="FileSystemPath"/> against a configured root directory and
/// re-validates that the resolved absolute path stays within it before
/// performing any I/O.
/// </summary>
/// <remarks>
/// This class re-resolves and re-validates the path on every call; it never
/// trusts that a caller already checked containment. A path that resolves
/// outside the configured root is refused with <see cref="FileReadDenied"/>
/// or <see cref="FileWriteDenied"/> regardless of any higher-level
/// authorization decision that already ran, which is the low-level
/// boundary re-enforcing the same effect a higher-level allow cannot widen.
/// </remarks>
public sealed class SandboxedFileSystem: IFileSystem
{
    private readonly string _root;
    private readonly long _maximumReadBytes;
    private readonly long _maximumWriteBytes;

    /// <summary>Initializes a new instance of the <see cref="SandboxedFileSystem"/> class.</summary>
    /// <param name="options">The validated sandbox configuration.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentException"><see cref="SandboxedFileSystemOptions.RootDirectory"/> is not an absolute path.</exception>
    public SandboxedFileSystem(IOptions<SandboxedFileSystemOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var root = options.Value.RootDirectory;
        if (string.IsNullOrWhiteSpace(root) || !Path.IsPathRooted(root))
        {
            throw new ArgumentException(
                "SandboxedFileSystemOptions.RootDirectory must be set to an absolute path.", nameof(options));
        }

        _root = AppendTrailingSeparator(Path.GetFullPath(root));
        _maximumReadBytes = options.Value.MaximumReadBytes;
        _maximumWriteBytes = options.Value.MaximumWriteBytes;
    }

    /// <inheritdoc/>
    public async Task<FileReadResult> ReadAsync(FileReadRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!TryResolve(request.Path, out var resolved))
        {
            return new FileReadDenied($"Path '{request.Path}' resolves outside the configured root.");
        }

        if (!File.Exists(resolved))
        {
            return new FileNotFound(request.Path);
        }

        try
        {
            var length = new FileInfo(resolved).Length;
            if (length > _maximumReadBytes)
            {
                return new FileReadDenied(
                    $"File is {length} bytes, exceeding the configured maximum of {_maximumReadBytes}.");
            }

            var content = await File.ReadAllTextAsync(resolved, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
            return new FileRead(content, length);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new FileReadFailed("The file could not be read.");
        }
    }

    /// <inheritdoc/>
    public async Task<FileWriteResult> WriteAsync(FileWriteRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!TryResolve(request.Path, out var resolved))
        {
            return new FileWriteDenied($"Path '{request.Path}' resolves outside the configured root.");
        }

        var contentBytes = Encoding.UTF8.GetByteCount(request.Content);
        if (contentBytes > _maximumWriteBytes)
        {
            return new FileWriteDenied(
                $"Content is {contentBytes} bytes, exceeding the configured maximum of {_maximumWriteBytes}.");
        }

        if (request.Mode == FileWriteMode.CreateNew && File.Exists(resolved))
        {
            return new FileAlreadyExists(request.Path);
        }

        try
        {
            var directory = Path.GetDirectoryName(resolved);
            if (!string.IsNullOrEmpty(directory))
            {
                _ = Directory.CreateDirectory(directory);
            }

            if (request.Mode == FileWriteMode.Append)
            {
                await File.AppendAllTextAsync(resolved, request.Content, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await File.WriteAllTextAsync(resolved, request.Content, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
            }

            return new FileWritten(contentBytes);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new FileWriteFailed("The file could not be written.");
        }
    }

    private bool TryResolve(FileSystemPath path, out string resolved)
    {
        resolved = Path.GetFullPath(Path.Combine(_root, path.Value));
        return resolved.StartsWith(_root, StringComparison.Ordinal);
    }

    private static string AppendTrailingSeparator(string path) =>
        path.EndsWith(Path.DirectorySeparatorChar) ? path : path + Path.DirectorySeparatorChar;
}
