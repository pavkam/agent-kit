// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem;

using System.Runtime.InteropServices;

using Microsoft.Win32.SafeHandles;

/// <summary>Shared POSIX file open helpers for operating-system file capabilities.</summary>
internal static partial class PosixFileOperations
{
    private const int _errorAccessDenied = 13;
    private const int _errorInvalidArgument = 22;
    private const int _errorNotDirectory = 20;
    private const int _errorNotFound = 2;
    private const int _linuxErrorTooManyLinks = 40;
    private const int _macOsErrorTooManyLinks = 62;
    private const int _openReadOnly = 0;

    /// <summary>Gets whether no-follow secure traversal is available on this host.</summary>
    internal static bool IsSecureTraversalSupported => OperatingSystem.IsLinux() || OperatingSystem.IsMacOS();

    /// <summary>Determines whether <paramref name="hostTargetPath"/> remains under <paramref name="hostRootPath"/>.</summary>
    internal static bool IsUnderRoot(string hostTargetPath, string hostRootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostTargetPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(hostRootPath);
        var full = Path.GetFullPath(hostTargetPath);
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(hostRootPath));
        return string.Equals(full, root, StringComparison.Ordinal)
            || full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    /// <summary>Determines whether a resolved target is consistent with, and confined to, one registered root.</summary>
    /// <param name="target">The resolved target whose host path and root-relative path were bound by authorization.</param>
    /// <param name="hostRootPath">The absolute host directory registered for the target's root.</param>
    /// <returns>True when the host path lies under the root and equals the root joined with the root-relative path.</returns>
    /// <remarks>Descriptor traversal follows the root-relative path, so a host path that names a different location than the bound relative path is refused rather than silently ignored.</remarks>
    internal static bool IsResolvedUnderRoot(ResolvedFileTarget target, string hostRootPath)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(hostRootPath);
        if (!IsUnderRoot(target.HostTargetPath, hostRootPath))
        {
            return false;
        }

        var expected = Path.GetFullPath(Path.Combine(hostRootPath, target.RelativePath.Value));
        return string.Equals(Path.GetFullPath(target.HostTargetPath), expected, StringComparison.Ordinal);
    }

    /// <summary>Opens one regular file for reading by descriptor-relative no-follow traversal.</summary>
    /// <param name="root">The absolute root directory the path is confined to.</param>
    /// <param name="relativePath">The root-relative path of the file.</param>
    /// <param name="cancellationToken">Cancels between traversal segments.</param>
    /// <returns>
    /// An open seekable read stream owned by the caller and the file length on success; otherwise a typed not-found,
    /// denied (symbolic link or inaccessible boundary), failed (not a regular seekable file or I/O error), or unsupported
    /// outcome.
    /// </returns>
    /// <remarks>
    /// The returned stream wraps the very descriptor that was validated, so the file read is the file that was opened;
    /// the path is never resolved a second time. The open uses <c>O_NONBLOCK</c> so a named pipe with no writer fails
    /// promptly instead of hanging the caller.
    /// </remarks>
    internal static PosixOpenReadResult TryOpenRegularFileReadOnly(
        string root,
        string relativePath,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        if (!IsSecureTraversalSupported)
        {
            return PosixOpenReadResult.Unsupported("Secure no-follow file traversal is unavailable on this platform.");
        }

        if (!TryOpenParentDirectory(root, relativePath, cancellationToken, out var parent, out var fileName, out var traversalError))
        {
            return ClassifyOpenError(traversalError);
        }

        using (parent)
        {
            var descriptor = OpenAt(
                parent.DangerousGetHandle().ToInt32(),
                fileName,
                _openReadOnly | NoFollowFlag | CloseOnExecFlag | NonBlockingFlag,
                0);
            if (descriptor < 0)
            {
                return ClassifyOpenError(Marshal.GetLastPInvokeError());
            }

            var handle = new SafeFileHandle(new IntPtr(descriptor), ownsHandle: true);
            FileStream stream;
            try
            {
                stream = new FileStream(handle, FileAccess.Read, bufferSize: 81920, isAsync: false);
            }
            catch
            {
                handle.Dispose();
                throw;
            }

            if (!stream.CanSeek)
            {
                stream.Dispose();
                return PosixOpenReadResult.Failed("The target is not a regular seekable file.");
            }

            return PosixOpenReadResult.Success(stream, stream.Length);
        }
    }

    private static PosixOpenReadResult ClassifyOpenError(int error) => error switch
    {
        _errorNotFound => PosixOpenReadResult.NotFound(),
        _ when IsBoundaryViolation(error) => PosixOpenReadResult.Denied("The target crosses a symbolic link or an inaccessible boundary."),
        _ => PosixOpenReadResult.Failed("The file could not be read."),
    };

    /// <summary>Determines whether a native error means the path crossed a symbolic link or an inaccessible boundary.</summary>
    /// <param name="error">The native <c>errno</c> value.</param>
    /// <returns>True for access denied, not-a-directory, and too-many-links (the no-follow refusal of a symbolic link).</returns>
    internal static bool IsBoundaryViolation(int error) =>
        error is _errorAccessDenied or _errorNotDirectory
        || error == (OperatingSystem.IsMacOS() ? _macOsErrorTooManyLinks : _linuxErrorTooManyLinks);

    /// <summary>Opens the parent directory of a root-relative path by descriptor-relative no-follow traversal.</summary>
    /// <param name="root">The absolute root directory; it is opened without following a final symbolic link.</param>
    /// <param name="relativePath">The root-relative path whose final segment names the target.</param>
    /// <param name="cancellationToken">Cancels between segments.</param>
    /// <param name="parent">The owned parent directory descriptor on success; an unowned empty handle otherwise.</param>
    /// <param name="fileName">The final path segment on success; empty otherwise.</param>
    /// <param name="error">Zero on success; otherwise the native <c>errno</c> of the failing step.</param>
    /// <returns>True when every directory segment was opened without crossing a symbolic link.</returns>
    /// <remarks>
    /// Each segment is opened with <c>openat</c> relative to the previous descriptor using no-follow directory flags, so no
    /// intermediate symbolic link is ever followed and a concurrent swap of an earlier segment cannot redirect the effect.
    /// </remarks>
    internal static bool TryOpenParentDirectory(
        string root,
        string relativePath,
        CancellationToken cancellationToken,
        out SafeFileHandle parent,
        out string fileName,
        out int error)
    {
        var segments = relativePath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments[^1] == ".")
        {
            parent = new SafeFileHandle(IntPtr.Zero, ownsHandle: false);
            fileName = "";
            error = _errorInvalidArgument;
            return false;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var descriptor = Open(root, _openReadOnly | DirectoryFlag | NoFollowFlag | CloseOnExecFlag, 0);
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

    /// <summary>Reads the POSIX permission and set-user/set-group/sticky bits of an open descriptor.</summary>
    /// <param name="descriptor">The non-owned native file descriptor to inspect.</param>
    /// <param name="mode">The mode bits (equivalent to the traditional <c>st_mode</c> permission nibbles) on success; zero otherwise.</param>
    /// <returns><see langword="true"/> when the mode was read successfully.</returns>
    /// <remarks>
    /// Delegates to <see cref="File.GetUnixFileMode(SafeFileHandle)"/> instead of reading raw <c>struct stat</c> offsets by
    /// hand: those offsets differ across architecture and OS combinations. <see cref="UnixFileMode"/>'s flag values are
    /// numerically identical to the traditional octal permission bits.
    /// </remarks>
    internal static bool TryGetFileMode(int descriptor, out int mode)
    {
        try
        {
            using var handle = new SafeFileHandle(descriptor, ownsHandle: false);
#pragma warning disable CA1416 // Every caller first checks IsSecureTraversalSupported, which is Linux or macOS only.
            mode = (int) File.GetUnixFileMode(handle);
#pragma warning restore CA1416
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            mode = 0;
            return false;
        }
    }

    /// <summary>Gets the <c>O_CLOEXEC</c> open flag for the current platform.</summary>
    internal static int CloseOnExecFlag => OperatingSystem.IsMacOS() ? 0x01000000 : 0x00080000;

    /// <summary>Gets the <c>O_CREAT</c> open flag for the current platform.</summary>
    internal static int CreateFlag => OperatingSystem.IsMacOS() ? 0x0200 : 0x0040;

    /// <summary>Gets the <c>O_DIRECTORY</c> open flag for the current platform.</summary>
    internal static int DirectoryFlag => PosixOpenFlags.Directory;

    /// <summary>Gets the <c>O_EXCL</c> open flag for the current platform.</summary>
    internal static int ExclusiveFlag => OperatingSystem.IsMacOS() ? 0x0800 : 0x0080;

    /// <summary>Gets the <c>O_NOFOLLOW</c> open flag for the current platform.</summary>
    internal static int NoFollowFlag => PosixOpenFlags.NoFollow;

    /// <summary>Gets the <c>O_NONBLOCK</c> open flag for the current platform.</summary>
    internal static int NonBlockingFlag => OperatingSystem.IsMacOS() ? 0x0004 : 0x0800;

    /// <summary>Gets the <c>O_APPEND</c> open flag.</summary>
    internal static int AppendFlag => 0x0008;

    /// <summary>Gets the <c>O_WRONLY</c> open flag.</summary>
    internal static int WriteOnlyFlag => 0x0001;

    [LibraryImport("libc", EntryPoint = "open", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int Open(string path, int flags, int mode);

    [LibraryImport("libc", EntryPoint = "openat", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int OpenAt(int directoryDescriptor, string path, int flags, int mode);

    [LibraryImport("libc", EntryPoint = "close", SetLastError = true)]
    internal static partial int CloseDescriptor(int descriptor);

    [LibraryImport("libc", EntryPoint = "fchmod", SetLastError = true)]
    internal static partial int ChangeMode(int descriptor, int mode);

    [LibraryImport("libc", EntryPoint = "renameat", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int RenameAt(
        int oldDirectoryDescriptor,
        string oldPath,
        int newDirectoryDescriptor,
        string newPath);

    [LibraryImport("libc", EntryPoint = "unlinkat", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int UnlinkAt(int directoryDescriptor, string path, int flags);

    internal readonly struct PosixOpenReadResult
    {
        private PosixOpenReadResult(
            PosixOpenReadStatus status,
            FileStream? stream,
            long length,
            string? message)
        {
            Status = status;
            Stream = stream;
            Length = length;
            Message = message;
        }

        internal PosixOpenReadStatus Status { get; }

        internal FileStream? Stream { get; }

        internal long Length { get; }

        internal string? Message { get; }

        internal static PosixOpenReadResult Success(FileStream stream, long length) =>
            new(PosixOpenReadStatus.Success, stream, length, null);

        internal static PosixOpenReadResult NotFound() =>
            new(PosixOpenReadStatus.NotFound, null, 0, null);

        internal static PosixOpenReadResult Denied(string message) =>
            new(PosixOpenReadStatus.Denied, null, 0, message);

        internal static PosixOpenReadResult Failed(string message) =>
            new(PosixOpenReadStatus.Failed, null, 0, message);

        internal static PosixOpenReadResult Unsupported(string message) =>
            new(PosixOpenReadStatus.Unsupported, null, 0, message);
    }

    internal enum PosixOpenReadStatus
    {
        Success,
        NotFound,
        Denied,
        Failed,
        Unsupported,
    }
}
