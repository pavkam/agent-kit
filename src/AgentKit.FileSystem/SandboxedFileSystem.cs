// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem;

using System.Runtime.InteropServices;

using Microsoft.Extensions.Options;
using Microsoft.Win32.SafeHandles;

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
public sealed partial class SandboxedFileSystem: IFileSystem
{
    private const int _errorAccessDenied = 13;
    private const int _errorAlreadyExists = 17;
    private const int _errorInvalidArgument = 22;
    private const int _errorNotDirectory = 20;
    private const int _errorNotFound = 2;
    private const int _linuxErrorTooManyLinks = 40;
    private const int _macOsErrorTooManyLinks = 62;
    private const int _openAppend = 0x0008;
    private const int _openReadOnly = 0;
    private const int _openWriteOnly = 0x0001;
    private const int _ownerReadWritePermissions = 0x0180;
    private const int _unixDirectoryPermissions = 0x01FF;
    private const int _unixFilePermissions = 0x01B6;
    private const int _writeOpenAttempts = 4;

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

        _root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        _maximumReadBytes = options.Value.MaximumReadBytes;
        _maximumWriteBytes = options.Value.MaximumWriteBytes;
    }

    /// <inheritdoc/>
    public async Task<FileReadResult> ReadAsync(FileReadRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (!IsSecureTraversalSupported)
        {
            return new FileReadDenied("Secure no-follow file traversal is unavailable on this platform.");
        }

        if (!TryOpenParentDirectory(
                request.Path,
                createMissingDirectories: false,
                cancellationToken,
                out var parent,
                out var fileName,
                out var traversalError))
        {
            return traversalError == _errorNotFound
                ? new FileNotFound(request.Path)
                : BoundaryReadFailure(request.Path, traversalError);
        }

        using (parent)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var descriptor = OpenAt(
                    parent.DangerousGetHandle().ToInt32(),
                    fileName,
                    _openReadOnly | NoFollowFlag | CloseOnExecFlag,
                    0);
                if (descriptor < 0)
                {
                    var error = Marshal.GetLastPInvokeError();
                    return error == _errorNotFound
                        ? new FileNotFound(request.Path)
                        : BoundaryReadFailure(request.Path, error);
                }

                using var handle = new SafeFileHandle(new IntPtr(descriptor), ownsHandle: true);
                await using var stream = new FileStream(handle, FileAccess.Read, bufferSize: 81920, isAsync: false);
                using var content = new MemoryStream();
                var buffer = new byte[81920];
                long totalBytes = 0;

                while (true)
                {
                    var bytesRead = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                    if (bytesRead == 0)
                    {
                        break;
                    }

                    totalBytes += bytesRead;
                    if (totalBytes > _maximumReadBytes)
                    {
                        return new FileReadDenied(
                            $"File exceeds the configured maximum of {_maximumReadBytes} bytes.");
                    }

                    await content.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
                }

                return new FileRead(Encoding.UTF8.GetString(content.GetBuffer(), 0, checked((int) content.Length)), totalBytes);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return new FileReadFailed("The file could not be read.");
            }
        }
    }

    /// <inheritdoc/>
    public async Task<FileWriteResult> WriteAsync(FileWriteRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfUndefined(request.Mode);
        cancellationToken.ThrowIfCancellationRequested();

        if (!IsSecureTraversalSupported)
        {
            return new FileWriteDenied("Secure no-follow file traversal is unavailable on this platform.");
        }

        var contentBytes = Encoding.UTF8.GetByteCount(request.Content);
        if (contentBytes > _maximumWriteBytes)
        {
            return new FileWriteDenied(
                $"Content is {contentBytes} bytes, exceeding the configured maximum of {_maximumWriteBytes}.");
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            _ = Directory.CreateDirectory(_root);

            if (!TryOpenParentDirectory(
                    request.Path,
                    createMissingDirectories: true,
                    cancellationToken,
                    out var parent,
                    out var fileName,
                    out var traversalError))
            {
                return BoundaryWriteFailure(request.Path, traversalError);
            }

            using (parent)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!TryOpenWriteTarget(
                    parent.DangerousGetHandle().ToInt32(),
                    fileName,
                    request.Mode,
                    out var descriptor,
                    out var created,
                    out var openError))
                {
                    return request.Mode == FileWriteMode.CreateNew && openError == _errorAlreadyExists
                        ? new FileAlreadyExists(request.Path)
                        : BoundaryWriteFailure(request.Path, openError);
                }

                using var handle = new SafeFileHandle(new IntPtr(descriptor), ownsHandle: true);
                if (created)
                {
                    _ = ChangeMode(descriptor, _ownerReadWritePermissions);
                }

                await using var stream = new FileStream(handle, FileAccess.Write, bufferSize: 81920, isAsync: false);
                if (request.Mode == FileWriteMode.Append)
                {
                    _ = stream.Seek(0, SeekOrigin.End);
                }

                var bytes = Encoding.UTF8.GetBytes(request.Content);
                await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            return new FileWritten(contentBytes);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new FileWriteFailed("The file could not be written.");
        }
    }

    private static bool IsSecureTraversalSupported => OperatingSystem.IsLinux() || OperatingSystem.IsMacOS();

    private static int CloseOnExecFlag => OperatingSystem.IsMacOS() ? 0x01000000 : 0x00080000;

    private static int CreateFlag => OperatingSystem.IsMacOS() ? 0x0200 : 0x0040;

    private static int DirectoryFlag => OperatingSystem.IsMacOS() ? 0x00100000 : 0x00010000;

    private static int ExclusiveFlag => OperatingSystem.IsMacOS() ? 0x0800 : 0x0080;

    private static int NoFollowFlag => OperatingSystem.IsMacOS() ? 0x0100 : 0x00020000;

    private static int TruncateFlag => OperatingSystem.IsMacOS() ? 0x0400 : 0x0200;

    private static bool IsBoundaryViolation(int error) =>
        error is _errorAccessDenied or _errorNotDirectory
        || error == (OperatingSystem.IsMacOS() ? _macOsErrorTooManyLinks : _linuxErrorTooManyLinks);

    private static FileReadResult BoundaryReadFailure(FileSystemPath path, int error) =>
        IsBoundaryViolation(error)
            ? new FileReadDenied($"Path '{path}' crosses a symbolic link or an inaccessible boundary.")
            : new FileReadFailed("The file could not be read.");

    private static FileWriteResult BoundaryWriteFailure(FileSystemPath path, int error) =>
        IsBoundaryViolation(error)
            ? new FileWriteDenied($"Path '{path}' crosses a symbolic link or an inaccessible boundary.")
            : new FileWriteFailed("The file could not be written.");

    private bool TryOpenParentDirectory(
        FileSystemPath path,
        bool createMissingDirectories,
        CancellationToken cancellationToken,
        out SafeFileHandle parent,
        out string fileName,
        out int error)
    {
        var segments = path.Value.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments[^1] == ".")
        {
            parent = new SafeFileHandle(IntPtr.Zero, ownsHandle: false);
            fileName = "";
            error = _errorInvalidArgument;
            return false;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var descriptor = Open(
            _root,
            _openReadOnly | DirectoryFlag | NoFollowFlag | CloseOnExecFlag,
            0);
        if (descriptor < 0)
        {
            parent = new SafeFileHandle(IntPtr.Zero, ownsHandle: false);
            fileName = "";
            error = Marshal.GetLastPInvokeError();
            return false;
        }

        var current = new SafeFileHandle(new IntPtr(descriptor), ownsHandle: true);
        for (var index = 0; index < segments.Length - 1; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            descriptor = OpenAt(
                current.DangerousGetHandle().ToInt32(),
                segments[index],
                _openReadOnly | DirectoryFlag | NoFollowFlag | CloseOnExecFlag,
                0);

            if (descriptor < 0 && createMissingDirectories && Marshal.GetLastPInvokeError() == _errorNotFound)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var createResult = MakeDirectoryAt(
                    current.DangerousGetHandle().ToInt32(),
                    segments[index],
                    _unixDirectoryPermissions);
                var createError = createResult < 0 ? Marshal.GetLastPInvokeError() : 0;
                if (createResult < 0 && createError != _errorAlreadyExists)
                {
                    current.Dispose();
                    parent = new SafeFileHandle(IntPtr.Zero, ownsHandle: false);
                    fileName = "";
                    error = createError;
                    return false;
                }

                cancellationToken.ThrowIfCancellationRequested();
                descriptor = OpenAt(
                    current.DangerousGetHandle().ToInt32(),
                    segments[index],
                    _openReadOnly | DirectoryFlag | NoFollowFlag | CloseOnExecFlag,
                    0);
            }

            if (descriptor < 0)
            {
                error = Marshal.GetLastPInvokeError();
                current.Dispose();
                parent = new SafeFileHandle(IntPtr.Zero, ownsHandle: false);
                fileName = "";
                return false;
            }

            var next = new SafeFileHandle(new IntPtr(descriptor), ownsHandle: true);
            current.Dispose();
            current = next;
        }

        parent = current;
        fileName = segments[^1];
        error = 0;
        return true;
    }

    private static bool TryOpenWriteTarget(
        int parentDescriptor,
        string fileName,
        FileWriteMode mode,
        out int descriptor,
        out bool created,
        out int error)
    {
        for (var attempt = 0; attempt < _writeOpenAttempts; attempt++)
        {
            descriptor = OpenAt(
                parentDescriptor,
                fileName,
                NewFileOpenFlags(mode),
                _unixFilePermissions);
            if (descriptor >= 0)
            {
                created = true;
                error = 0;
                return true;
            }

            error = Marshal.GetLastPInvokeError();
            if (mode == FileWriteMode.CreateNew || error != _errorAlreadyExists)
            {
                created = false;
                return false;
            }

            descriptor = OpenAt(parentDescriptor, fileName, ExistingFileOpenFlags(mode), 0);
            if (descriptor >= 0)
            {
                created = false;
                error = 0;
                return true;
            }

            error = Marshal.GetLastPInvokeError();
            if (error != _errorNotFound)
            {
                created = false;
                return false;
            }
        }

        descriptor = -1;
        created = false;
        error = _errorNotFound;
        return false;
    }

    private static int NewFileOpenFlags(FileWriteMode mode) => mode switch
    {
        FileWriteMode.CreateOrOverwrite =>
            _openWriteOnly | CreateFlag | ExclusiveFlag | TruncateFlag | NoFollowFlag | CloseOnExecFlag,
        FileWriteMode.CreateNew => _openWriteOnly | CreateFlag | ExclusiveFlag | NoFollowFlag | CloseOnExecFlag,
        FileWriteMode.Append =>
            _openWriteOnly | CreateFlag | ExclusiveFlag | _openAppend | NoFollowFlag | CloseOnExecFlag,
        _ => throw new System.Diagnostics.UnreachableException()
    };

    private static int ExistingFileOpenFlags(FileWriteMode mode) => mode switch
    {
        FileWriteMode.CreateOrOverwrite => _openWriteOnly | TruncateFlag | NoFollowFlag | CloseOnExecFlag,
        FileWriteMode.Append => _openWriteOnly | _openAppend | NoFollowFlag | CloseOnExecFlag,
        FileWriteMode.CreateNew => throw new NotImplementedException(),
        _ => throw new System.Diagnostics.UnreachableException()
    };

    [LibraryImport("libc", EntryPoint = "fchmod", SetLastError = true)]
    private static partial int ChangeMode(int descriptor, int mode);

    [LibraryImport("libc", EntryPoint = "open", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int Open(string path, int flags, int mode);

    [LibraryImport("libc", EntryPoint = "openat", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int OpenAt(int directoryDescriptor, string path, int flags, int mode);

    [LibraryImport("libc", EntryPoint = "mkdirat", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int MakeDirectoryAt(int directoryDescriptor, string path, int mode);
}
