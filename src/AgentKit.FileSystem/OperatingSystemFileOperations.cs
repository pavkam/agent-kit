// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem;

/// <summary>Grant, audit, and host open orchestration for operating-system file reads.</summary>
internal static class OperatingSystemFileOperations
{
    /// <summary>Opens one authorized read against the host file system.</summary>
    internal static async ValueTask<FileReadOpenResult> OpenReadAsync(
        AuthorizedFileRead operation,
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
        if (root is null || !PosixFileOperations.IsUnderRoot(operation.ResolvedTarget.HostTargetPath, root.HostRootPath))
        {
            return new FileReadOpenDenied("The resolved target is outside the configured file root.");
        }

        var enforcement = FileSystemEnforcementReceipt.Create(
            operation.Grant,
            audience,
            SecurityOperationKind.FileRead,
            SecurityEffect.Observe,
            [FileSecurityBinding.Resource(operation.ResolvedTarget)],
            FileSecurityBinding.ReadFingerprint(operation.Request));
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
            return new FileReadOpenDenied(denial);
        }

        var effectiveMax = Math.Min(
            operation.Request.Bounds.MaxBytes,
            options.Bounds.MaximumReadBytes);
        var probe = PosixFileOperations.TryOpenRegularFileReadOnly(operation.ResolvedTarget.HostTargetPath);
        return probe.Status switch
        {
            PosixFileOperations.PosixOpenReadStatus.NotFound => new FileReadOpenNotFound(),
            PosixFileOperations.PosixOpenReadStatus.Denied => new FileReadOpenDenied(probe.Message!),
            PosixFileOperations.PosixOpenReadStatus.Unsupported => new FileReadOpenFailed(probe.Message!),
            PosixFileOperations.PosixOpenReadStatus.Failed => new FileReadOpenFailed(probe.Message!),
            PosixFileOperations.PosixOpenReadStatus.Success => OpenBoundedHandle(
                operation.ResolvedTarget.HostTargetPath,
                probe.Length,
                effectiveMax),
            _ => new FileReadOpenFailed("The file could not be read."),
        };
    }

    private static FileReadHandleOpened OpenBoundedHandle(string hostTargetPath, long snapshotLength, long effectiveMax)
    {
        FileStream fileStream;
        try
        {
            fileStream = new FileStream(
                hostTargetPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
        }
        catch (FileNotFoundException)
        {
            throw new UnreachableException("The target was observed then disappeared before the read handle opened.");
        }
        catch (IOException)
        {
            throw new UnreachableException("The target could not be reopened after authorization.");
        }

        var bounded = new OperatingSystemBoundedReadStream(fileStream, snapshotLength, effectiveMax);
        var metadata = new FileMetadata(snapshotLength, lastModifiedUtc: null, contentFingerprint: null);
        return new FileReadHandleOpened(new OperatingSystemFileReadHandle(metadata, bounded));
    }
}
