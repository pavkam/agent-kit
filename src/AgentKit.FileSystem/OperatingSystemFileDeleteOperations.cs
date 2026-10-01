// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem;

using System.Runtime.InteropServices;

using Microsoft.Win32.SafeHandles;

/// <summary>Grant, audit, and host commit orchestration for operating-system file deletion.</summary>
/// <remarks>
/// Deletion walks to the target's parent by descriptor-relative no-follow traversal, observes the target without following a final
/// symbolic link, verifies it is a regular file that still satisfies the authorized fingerprint precondition, and removes it with a
/// descriptor-relative <c>unlinkat</c> that never follows the final component and never removes a directory.
/// </remarks>
internal static class OperatingSystemFileDeleteOperations
{
    private const int _errorNotFound = 2;
    private const int _readOnlyFlag = 0;

    /// <summary>Removes one authorized regular file.</summary>
    internal static async ValueTask<FileDeleteResult> DeleteAsync(
        AuthorizedFileDelete operation,
        ISecurityGrantStore grantStore,
        ISecurityAuditDispatcher auditDispatcher,
        IIdentifierGenerator<SecurityAuditRecordId> auditRecordIds,
        IIdentifierGenerator<SecurityEnforcementIntentId> intentIds,
        TimeProvider timeProvider,
        OperatingSystemFileSystemOptionsSnapshot options,
        ComponentId audience,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        cancellationToken.ThrowIfCancellationRequested();

        var root = options.Roots.FirstOrDefault(r => r.RootId == operation.ResolvedTarget.RootId);
        if (root is null || !PosixFileOperations.IsResolvedUnderRoot(operation.ResolvedTarget, root.HostRootPath))
        {
            return new FileDeleteDenied("The resolved target is outside the configured file root.");
        }

        if (!PosixFileOperations.IsSecureTraversalSupported)
        {
            return new FileDeleteFailed("Secure no-follow file traversal is unavailable on this platform.");
        }

        var enforcement = FileSystemEnforcementReceipt.Create(
            operation.Grant,
            audience,
            SecurityOperationKind.FileWrite,
            SecurityEffect.Delete,
            [FileSecurityBinding.Resource(operation.ResolvedTarget)],
            FileSecurityBinding.DeleteFingerprint(operation));
        var intent = new SecurityEnforcementIntent(intentIds.Create(), null);
        var denial = await FileSystemHostGuard.ConsumeWithRequiredAuditAsync(
            operation.Grant,
            enforcement,
            intent,
            grantStore,
            auditDispatcher,
            auditRecordIds,
            timeProvider,
            cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (denial is not null)
        {
            return new FileDeleteDenied(denial);
        }

        if (!PosixFileOperations.TryOpenParentDirectory(
                root.HostRootPath,
                operation.ResolvedTarget.RelativePath.Value,
                cancellationToken,
                out var parent,
                out var fileName,
                out var traversalError))
        {
            return traversalError == _errorNotFound
                ? new FileDeleteNotFound(operation.ResolvedTarget)
                : Failure(traversalError);
        }

        using (parent)
        {
            try
            {
                return await DeleteWithinParentAsync(operation, parent.DangerousGetHandle().ToInt32(), fileName, options, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return new FileDeleteFailed("The file could not be deleted.");
            }
        }
    }

    private static FileDeleteResult Failure(int error) => PosixFileOperations.IsBoundaryViolation(error)
        ? new FileDeleteDenied("The target crosses a symbolic link or an inaccessible boundary.")
        : new FileDeleteFailed("The file could not be deleted.");

    private static async ValueTask<FileDeleteResult> DeleteWithinParentAsync(
        AuthorizedFileDelete operation,
        int parent,
        string fileName,
        OperatingSystemFileSystemOptionsSnapshot options,
        CancellationToken cancellationToken)
    {
        var descriptor = PosixFileOperations.OpenAt(
            parent,
            fileName,
            _readOnlyFlag | PosixFileOperations.NoFollowFlag | PosixFileOperations.CloseOnExecFlag | PosixFileOperations.NonBlockingFlag,
            0);
        if (descriptor < 0)
        {
            var openError = Marshal.GetLastPInvokeError();
            return openError == _errorNotFound ? new FileDeleteNotFound(operation.ResolvedTarget) : Failure(openError);
        }

        long length;
        var handle = new SafeFileHandle(new IntPtr(descriptor), ownsHandle: true);
        await using (var stream = new FileStream(handle, FileAccess.Read, bufferSize: 81920, isAsync: false))
        {
            if (!stream.CanSeek)
            {
                return new FileDeleteConflict(operation.ResolvedTarget, "The target is not a regular file.");
            }

            length = stream.Length;
            if (operation.ExpectedTargetFingerprint is { } expected)
            {
                if (length > options.Bounds.MaximumReadBytes)
                {
                    return new FileDeleteFailed("The target is too large for its fingerprint precondition to be verified.");
                }

                using var content = new MemoryStream(capacity: checked((int) length));
                await stream.CopyToAsync(content, cancellationToken).ConfigureAwait(false);
                if (FileSecurityBinding.ContentFingerprint(content.GetBuffer().AsSpan(0, checked((int) content.Length))) != expected)
                {
                    return new FileDeleteConflict(operation.ResolvedTarget, "The target fingerprint did not match the required precondition.");
                }
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (PosixFileOperations.UnlinkAt(parent, fileName, 0) < 0)
        {
            var unlinkError = Marshal.GetLastPInvokeError();
            return unlinkError == _errorNotFound ? new FileDeleteNotFound(operation.ResolvedTarget) : Failure(unlinkError);
        }

        return new FileDeleteSuccess(operation.ResolvedTarget, length);
    }
}
