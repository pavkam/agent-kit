// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem;

using System.Runtime.InteropServices;

using Microsoft.Win32.SafeHandles;

/// <summary>Shared POSIX file open helpers for operating-system file capabilities.</summary>
internal static partial class PosixFileOperations
{
    private const int _errorAccessDenied = 13;
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

    /// <summary>Opens one regular file for bounded reading without following symbolic links.</summary>
    internal static PosixOpenReadResult TryOpenRegularFileReadOnly(string absolutePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(absolutePath);
        if (!IsSecureTraversalSupported)
        {
            return PosixOpenReadResult.Unsupported("Secure no-follow file traversal is unavailable on this platform.");
        }

        var descriptor = Open(
            Path.GetFullPath(absolutePath),
            _openReadOnly | NoFollowFlag | CloseOnExecFlag | NonBlockingFlag,
            0);
        if (descriptor < 0)
        {
            var error = Marshal.GetLastPInvokeError();
            return error switch
            {
                _errorNotFound => PosixOpenReadResult.NotFound(),
                var boundary when IsBoundaryViolation(boundary) => PosixOpenReadResult.Denied(
                    "The target crosses a symbolic link or an inaccessible boundary."),
                _ => PosixOpenReadResult.Failed("The file could not be read."),
            };
        }

        var handle = new SafeFileHandle(new IntPtr(descriptor), ownsHandle: true);
        try
        {
            var length = RandomAccess.GetLength(handle);
            handle.Dispose();
            return length < 0
                ? PosixOpenReadResult.Failed("The target is not a regular seekable file.")
                : PosixOpenReadResult.Success(null, length);
        }
        catch (IOException)
        {
            handle.Dispose();
            return PosixOpenReadResult.Failed("The target is not a regular seekable file.");
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    private static bool IsBoundaryViolation(int error) =>
        error is _errorAccessDenied or _errorNotDirectory
        || error == (OperatingSystem.IsMacOS() ? _macOsErrorTooManyLinks : _linuxErrorTooManyLinks);

    private static int CloseOnExecFlag => OperatingSystem.IsMacOS() ? 0x01000000 : 0x00080000;

    private static int NoFollowFlag => PosixOpenFlags.NoFollow;

    private static int NonBlockingFlag => OperatingSystem.IsMacOS() ? 0x0004 : 0x0800;

    [LibraryImport("libc", EntryPoint = "open", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int Open(string path, int flags, int mode);

    /// <summary>Closed result of a POSIX regular-file open attempt.</summary>
    internal readonly struct PosixOpenReadResult
    {
        private PosixOpenReadResult(
            PosixOpenReadStatus status,
            SafeFileHandle? handle,
            long length,
            string? message)
        {
            Status = status;
            Handle = handle;
            Length = length;
            Message = message;
        }

        internal PosixOpenReadStatus Status { get; }

        internal SafeFileHandle? Handle { get; }

        internal long Length { get; }

        internal string? Message { get; }

        internal static PosixOpenReadResult Success(SafeFileHandle? handle, long length) =>
            new(PosixOpenReadStatus.Success, handle, length, null);

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
