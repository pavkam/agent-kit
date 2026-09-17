// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem;

using System.Runtime.InteropServices;

using Microsoft.Win32.SafeHandles;

public sealed partial class SandboxedFileSystem
{
    private readonly Lock _mutationLockGate = new();
    private readonly Dictionary<string, MutationLockEntry> _mutationLocks = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    private async ValueTask<FileSnapshotResult> ReadSnapshotCoreAsync(
        FileSnapshotRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.MaximumBytes);
        ArgumentNullException.ThrowIfNull(request.Grant);
        cancellationToken.ThrowIfCancellationRequested();

        var enforcement = FileSystemEnforcementReceipt.Create(
            request.Grant,
            SecurityAudience,
            SecurityOperationKind.FileRead,
            SecurityEffect.Observe,
            [FileSecurityBinding.Resource(request.Path)],
            FileSecurityBinding.SnapshotFingerprint(request.Path, request.MaximumBytes));
        var enforcementIntent = new SecurityEnforcementIntent(_intentIds.Create(), null);
        var grantResult = await _grantStore.ValidateAndConsumeAsync(
            request.Grant, enforcement, enforcementIntent, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (!FileSystemEnforcementReceipt.IsFreshExact(grantResult, request.Grant, enforcement, enforcementIntent))
        {
            return SnapshotFailure(FileSnapshotStatus.Denied, FileSystemEnforcementReceipt.DenialMessage(grantResult));
        }

        if (request.MaximumBytes > _maximumReadBytes)
        {
            return SnapshotFailure(FileSnapshotStatus.Denied, "The snapshot exceeds a configured host boundary.");
        }

        if (!IsSecureTraversalSupported)
        {
            return SnapshotFailure(FileSnapshotStatus.Denied, "Secure no-follow traversal is unavailable on this platform.");
        }

        if (!TryOpenParentDirectory(
                request.Path,
                cancellationToken,
                out var parent,
                out var fileName,
                out var traversalError))
        {
            return traversalError == _errorNotFound
                ? SnapshotFailure(FileSnapshotStatus.NotFound, "The snapshot target does not exist.")
                : SnapshotBoundaryFailure(traversalError);
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
                var error = Marshal.GetLastPInvokeError();
                return error == _errorNotFound
                    ? SnapshotFailure(FileSnapshotStatus.NotFound, "The snapshot target does not exist.")
                    : SnapshotBoundaryFailure(error);
            }

            using var handle = new SafeFileHandle(new IntPtr(descriptor), ownsHandle: true);
            return await ReadSnapshotFromHandleAsync(handle, request.MaximumBytes, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    private async ValueTask<AtomicFileReplaceResult> ReplaceCoreAsync(
        AtomicFileReplaceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfDefault(request.Content);
        ArgumentNullException.ThrowIfNull(request.Grant);
        cancellationToken.ThrowIfCancellationRequested();

        if (request.Content.Length > _maximumWriteBytes)
        {
            return ReplaceFailure(AtomicFileReplaceStatus.Denied, "The replacement exceeds a configured host boundary.");
        }

        if (!IsSecureTraversalSupported)
        {
            return ReplaceFailure(
                AtomicFileReplaceStatus.Denied, "Secure no-follow replacement is unavailable on this platform.");
        }

        using (await AcquireMutationLockAsync(request.Path.Value, cancellationToken).ConfigureAwait(false))
        {
            var enforcement = FileSystemEnforcementReceipt.Create(
                request.Grant,
                SecurityAudience,
                SecurityOperationKind.FileWrite,
                SecurityEffect.Replace,
                FileSecurityBinding.AtomicReplaceResources(request.Id, request.Path),
                FileSecurityBinding.AtomicReplaceFingerprint(
                    request.Id, request.Path, request.ExpectedContentFingerprint, request.Content));
            var enforcementIntent = new SecurityEnforcementIntent(_intentIds.Create(), null);
            var grantResult = await _grantStore.ValidateAndConsumeAsync(
                request.Grant, enforcement, enforcementIntent, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (!FileSystemEnforcementReceipt.IsFreshExact(grantResult, request.Grant, enforcement, enforcementIntent))
            {
                return ReplaceFailure(AtomicFileReplaceStatus.Denied, FileSystemEnforcementReceipt.DenialMessage(grantResult));
            }

            if (!TryOpenParentDirectory(
                    request.Path,
                    cancellationToken,
                    out var parent,
                    out var fileName,
                    out var traversalError))
            {
                return traversalError == _errorNotFound
                    ? ReplaceFailure(AtomicFileReplaceStatus.NotFound, "The replacement target does not exist.")
                    : ReplaceBoundaryFailure(traversalError);
            }

            using (parent)
            {
                return await ReplaceWithinParentAsync(
                    parent,
                    fileName,
                    request,
                    cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async ValueTask<AtomicFileReplaceResult> ReplaceWithinParentAsync(
        SafeFileHandle parent,
        string fileName,
        AtomicFileReplaceRequest request,
        CancellationToken cancellationToken)
    {
        var currentDescriptor = OpenAt(
            parent.DangerousGetHandle().ToInt32(),
            fileName,
            _openReadOnly | NoFollowFlag | CloseOnExecFlag | NonBlockingFlag,
            0);
        if (currentDescriptor < 0)
        {
            var error = Marshal.GetLastPInvokeError();
            return error == _errorNotFound
                ? ReplaceFailure(AtomicFileReplaceStatus.NotFound, "The replacement target does not exist.")
                : ReplaceBoundaryFailure(error);
        }

        int mode;
        FileSnapshotResult snapshot;
        using (var currentHandle = new SafeFileHandle(new IntPtr(currentDescriptor), ownsHandle: true))
        {
            if (!TryGetFileMode(currentDescriptor, out mode))
            {
                return ReplaceFailure(AtomicFileReplaceStatus.Failed, "The target mode could not be observed.");
            }

            snapshot = await ReadSnapshotFromHandleAsync(
                currentHandle, _maximumReadBytes, cancellationToken).ConfigureAwait(false);
        }

        if (snapshot.Status != FileSnapshotStatus.Success)
        {
            return snapshot.Status == FileSnapshotStatus.LimitExceeded
                ? ReplaceFailure(AtomicFileReplaceStatus.Failed, "The current target exceeds the edit input boundary.")
                : ReplaceFailure(AtomicFileReplaceStatus.Failed, snapshot.SafeMessage ?? "The target could not be verified.");
        }

        if (snapshot.ContentFingerprint != request.ExpectedContentFingerprint)
        {
            return ReplaceFailure(AtomicFileReplaceStatus.Conflict, "The target changed after the edit was planned.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var stagingPath = FileSecurityBinding.AtomicReplaceStagingPath(request.Id, request.Path);
        var separator = stagingPath.Value.LastIndexOf('/');
        var stagingName = separator < 0 ? stagingPath.Value : stagingPath.Value[(separator + 1)..];
        var stagingDescriptor = OpenAt(
            parent.DangerousGetHandle().ToInt32(),
            stagingName,
            _openWriteOnly | CreateFlag | ExclusiveFlag | NoFollowFlag | CloseOnExecFlag,
            _ownerReadWritePermissions);
        if (stagingDescriptor < 0)
        {
            return ReplaceFailure(AtomicFileReplaceStatus.Failed, "The private staging file could not be created.");
        }

        var stagingExists = true;
        try
        {
            using (var stagingHandle = new SafeFileHandle(new IntPtr(stagingDescriptor), ownsHandle: true))
            await using (var stream = new FileStream(
                stagingHandle, FileAccess.Write, bufferSize: 81920, isAsync: false))
            {
                if (ChangeMode(stagingDescriptor, mode & 0x0FFF) < 0)
                {
                    return ReplaceFailure(AtomicFileReplaceStatus.Failed, "The target mode could not be preserved.");
                }

                await stream.WriteAsync(request.Content.AsMemory(), cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (RenameAt(
                    parent.DangerousGetHandle().ToInt32(),
                    stagingName,
                    parent.DangerousGetHandle().ToInt32(),
                    fileName) < 0)
            {
                return ReplaceFailure(AtomicFileReplaceStatus.Failed, "The staged replacement could not be committed.");
            }

            stagingExists = false;
            var fingerprint = FileSecurityBinding.ContentFingerprint(request.Content.AsSpan());
            var durable = Synchronize(parent.DangerousGetHandle().ToInt32()) == 0;
            return new AtomicFileReplaceResult(
                AtomicFileReplaceStatus.Committed,
                fingerprint,
                request.Content.Length,
                durable ? null : "The replacement committed, but directory durability could not be confirmed.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return ReplaceFailure(AtomicFileReplaceStatus.Failed, "The replacement could not be staged.");
        }
        finally
        {
            if (stagingExists)
            {
                _ = UnlinkAt(parent.DangerousGetHandle().ToInt32(), stagingName, 0);
            }
        }
    }

    private static async ValueTask<FileSnapshotResult> ReadSnapshotFromHandleAsync(
        SafeFileHandle handle,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = new FileStream(handle, FileAccess.Read, bufferSize: 81920, isAsync: false);
            if (!stream.CanSeek)
            {
                return SnapshotFailure(FileSnapshotStatus.Failed, "The snapshot target is not a regular seekable file.");
            }

            var length = stream.Length;
            if (length > maximumBytes || length > int.MaxValue)
            {
                return SnapshotFailure(FileSnapshotStatus.LimitExceeded, "The snapshot target exceeds its byte bound.");
            }

            var bytes = new byte[checked((int) length)];
            await stream.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);
            if (stream.Length != length)
            {
                return SnapshotFailure(FileSnapshotStatus.Changed, "The snapshot target changed while it was read.");
            }

            var immutable = ImmutableArray.CreateRange(bytes);
            return new FileSnapshotResult(
                FileSnapshotStatus.Success,
                immutable,
                FileSecurityBinding.ContentFingerprint(immutable.AsSpan()),
                null);
        }
        catch (Exception exception) when (exception is IOException or NotSupportedException or UnauthorizedAccessException)
        {
            return SnapshotFailure(FileSnapshotStatus.Failed, "The snapshot target could not be read.");
        }
    }

    /// <summary>Reads the POSIX permission and set-user/set-group/sticky bits of an open descriptor.</summary>
    /// <param name="descriptor">The non-owned native file descriptor to inspect.</param>
    /// <param name="mode">The mode bits (equivalent to the traditional <c>st_mode</c> permission nibbles) on success; zero otherwise.</param>
    /// <returns><see langword="true"/> when the mode was read successfully.</returns>
    /// <remarks>
    /// Delegates to <see cref="File.GetUnixFileMode(SafeFileHandle)"/> instead of reading raw
    /// <c>struct stat</c> offsets by hand: those offsets differ across architecture/OS
    /// combinations (for example <c>st_mode</c> sits at a different offset on x86_64 macOS than on
    /// arm64 macOS, and differently again on aarch64 Linux than on x86_64 Linux), so a hard-coded
    /// offset silently reads the wrong field on some of them. <see cref="UnixFileMode"/>'s flag
    /// values are numerically identical to the traditional octal permission bits, so casting it to
    /// <see langword="int"/> reproduces the value this method previously read directly.
    /// </remarks>
    private static bool TryGetFileMode(int descriptor, out int mode)
    {
        try
        {
            using var handle = new SafeFileHandle(descriptor, ownsHandle: false);
#pragma warning disable CA1416 // This whole type is Unix-only (every sibling member here is a raw libc P/Invoke without separate platform attribution).
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

    private static FileSnapshotResult SnapshotBoundaryFailure(int error) => IsBoundaryViolation(error)
        ? SnapshotFailure(FileSnapshotStatus.Denied, "The snapshot path crosses an inaccessible boundary.")
        : SnapshotFailure(FileSnapshotStatus.Failed, "The snapshot target could not be read.");

    private static FileSnapshotResult SnapshotFailure(FileSnapshotStatus status, string message) =>
        new(status, [], null, message);

    private static AtomicFileReplaceResult ReplaceBoundaryFailure(int error) => IsBoundaryViolation(error)
        ? ReplaceFailure(AtomicFileReplaceStatus.Denied, "The replacement path crosses an inaccessible boundary.")
        : ReplaceFailure(AtomicFileReplaceStatus.Failed, "The replacement target could not be opened.");

    private static AtomicFileReplaceResult ReplaceFailure(AtomicFileReplaceStatus status, string message) =>
        new(status, null, 0, message);

    private async ValueTask<MutationLockLease> AcquireMutationLockAsync(
        string path,
        CancellationToken cancellationToken)
    {
        MutationLockEntry entry;
        lock (_mutationLockGate)
        {
            if (!_mutationLocks.TryGetValue(path, out entry!))
            {
                entry = new MutationLockEntry();
                _mutationLocks.Add(path, entry);
            }

            entry.References++;
        }

        try
        {
            await entry.Semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            return new MutationLockLease(this, path, entry);
        }
        catch
        {
            ReleaseMutationLockReference(path, entry, releaseSemaphore: false);
            throw;
        }
    }

    private void ReleaseMutationLockReference(
        string path,
        MutationLockEntry entry,
        bool releaseSemaphore)
    {
        if (releaseSemaphore)
        {
            _ = entry.Semaphore.Release();
        }

        lock (_mutationLockGate)
        {
            entry.References--;
            Debug.Assert(entry.References >= 0, "Mutation lock references never become negative.");
            if (entry.References == 0)
            {
                _ = _mutationLocks.Remove(path);
                entry.Semaphore.Dispose();
            }
        }
    }

    private sealed class MutationLockEntry
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);
        public int References { get; set; }
    }

    private sealed class MutationLockLease(
        SandboxedFileSystem owner,
        string path,
        MutationLockEntry entry): IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            owner.ReleaseMutationLockReference(path, entry, releaseSemaphore: true);
        }
    }

    [LibraryImport("libc", EntryPoint = "fsync", SetLastError = true)]
    private static partial int Synchronize(int descriptor);

    [LibraryImport("libc", EntryPoint = "renameat", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int RenameAt(
        int oldDirectoryDescriptor,
        string oldPath,
        int newDirectoryDescriptor,
        string newPath);

    [LibraryImport("libc", EntryPoint = "unlinkat", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int UnlinkAt(int directoryDescriptor, string path, int flags);
}
