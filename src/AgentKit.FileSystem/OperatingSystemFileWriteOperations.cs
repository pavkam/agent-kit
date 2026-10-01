// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem;

using System.Runtime.InteropServices;

using Microsoft.Win32.SafeHandles;

/// <summary>Grant, audit, and host commit orchestration for operating-system file writes.</summary>
internal static class OperatingSystemFileWriteOperations
{
    private const int _errorAlreadyExists = 17;
    private const int _errorNotFound = 2;
    private const int _ownerReadWriteMode = 0x0180;
    private const int _newFileCreationMode = 0x01B6;
    private const int _permissionBitsMask = 0x0FFF;
    private const int _readOnlyFlag = 0;
    private const int _writeAttempts = 4;

    /// <summary>Commits one authorized write under the declared disposition.</summary>
    internal static async ValueTask<FileWriteResult> WriteAsync(
        AuthorizedFileWrite operation,
        FileWriteContent content,
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
        ArgumentNullException.ThrowIfNull(content);
        cancellationToken.ThrowIfCancellationRequested();

        var root = options.Roots.FirstOrDefault(r => r.RootId == operation.ResolvedTarget.RootId);
        if (root is null || !PosixFileOperations.IsResolvedUnderRoot(operation.ResolvedTarget, root.HostRootPath))
        {
            return new FileWriteDenied("The resolved target is outside the configured file root.");
        }

        if (!PosixFileOperations.IsSecureTraversalSupported)
        {
            return new FileWriteFailed("Secure no-follow file traversal is unavailable on this platform.");
        }

        var payload = content.Payload;
        var payloadBytes = payload.Length;
        if (payloadBytes > options.Bounds.MaximumWriteBytes)
        {
            return new FileWriteLimitExceeded(options.Bounds.MaximumWriteBytes, payloadBytes);
        }

        var actualPayloadFingerprint = FileSecurityBinding.ContentFingerprint(payload.Span);
        if (actualPayloadFingerprint != content.PayloadFingerprint)
        {
            return new FileWriteFailed("The payload fingerprint did not match the supplied content.");
        }

        if (payloadBytes != operation.DeclaredContentLength
            || actualPayloadFingerprint != operation.DeclaredContentFingerprint)
        {
            return new FileWriteFailed("The payload did not match the declared write evidence.");
        }

        var enforcement = FileSystemEnforcementReceipt.Create(
            operation.Grant,
            audience,
            SecurityOperationKind.FileWrite,
            FileSecurityBinding.WriteEffect(operation.Disposition),
            [FileSecurityBinding.Resource(operation.ResolvedTarget)],
            FileSecurityBinding.WriteFingerprint(operation, actualPayloadFingerprint));
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
            return new FileWriteDenied(denial);
        }

        if (!PosixFileOperations.TryOpenParentDirectory(
                root.HostRootPath,
                operation.ResolvedTarget.RelativePath.Value,
                cancellationToken,
                out var parent,
                out var fileName,
                out var traversalError))
        {
            return TraversalFailure(operation, traversalError);
        }

        using (parent)
        {
            return await WriteWithinParentAsync(
                operation,
                payload,
                actualPayloadFingerprint,
                parent.DangerousGetHandle().ToInt32(),
                fileName,
                intent.Id,
                options,
                cancellationToken).ConfigureAwait(false);
        }
    }

    private static async ValueTask<FileWriteResult> WriteWithinParentAsync(
        AuthorizedFileWrite operation,
        ReadOnlyMemory<byte> payload,
        ContentHash payloadFingerprint,
        int parent,
        string fileName,
        SecurityEnforcementIntentId intentId,
        OperatingSystemFileSystemOptionsSnapshot options,
        CancellationToken cancellationToken)
    {
        try
        {
            var observed = await ObserveTargetAsync(parent, fileName, options.Bounds.MaximumReadBytes, cancellationToken)
                .ConfigureAwait(false);
            if (observed.Failure is { } observationFailure)
            {
                return observationFailure;
            }

            var exists = observed.Exists;
            if (operation.ExpectedTargetFingerprint is { } expected)
            {
                if (!exists)
                {
                    return new FileWriteNotFound(operation.ResolvedTarget);
                }

                if (observed.Fingerprint is not { } current)
                {
                    return new FileWriteFailed("The target is too large for its fingerprint precondition to be verified.");
                }

                if (current != expected)
                {
                    return new FileWriteConflict(
                        operation.ResolvedTarget,
                        "The target fingerprint did not match the required precondition.");
                }
            }

            return operation.Disposition switch
            {
                FileWriteDisposition.CreateOnly when exists => new FileWriteConflict(operation.ResolvedTarget, "The target already exists."),
                FileWriteDisposition.CreateOnly => await CreateAsync(operation, parent, fileName, payload, payloadFingerprint, cancellationToken).ConfigureAwait(false),
                FileWriteDisposition.ReplaceExisting when !exists => new FileWriteNotFound(operation.ResolvedTarget),
                FileWriteDisposition.ReplaceExisting => await ReplaceAsync(operation, parent, fileName, payload, payloadFingerprint, observed.Length, intentId, cancellationToken).ConfigureAwait(false),
                FileWriteDisposition.CreateOrReplace => await CreateOrReplaceAsync(operation, parent, fileName, payload, payloadFingerprint, observed.Length, intentId, cancellationToken).ConfigureAwait(false),
                FileWriteDisposition.Append when !exists => new FileWriteNotFound(operation.ResolvedTarget),
                FileWriteDisposition.Append => await AppendAsync(operation, parent, fileName, payload, payloadFingerprint, observed.Length, options, cancellationToken).ConfigureAwait(false),
                _ => new FileWriteFailed("The write disposition is not supported."),
            };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new FileWriteFailed("The file could not be written.");
        }
    }

    private static FileWriteResult TraversalFailure(AuthorizedFileWrite operation, int error) => error switch
    {
        _ when error == _errorNotFound => operation.Disposition is FileWriteDisposition.ReplaceExisting or FileWriteDisposition.Append
            ? new FileWriteNotFound(operation.ResolvedTarget)
            : new FileWriteFailed("A parent directory does not exist."),
        _ when PosixFileOperations.IsBoundaryViolation(error) => new FileWriteDenied("The target crosses a symbolic link or an inaccessible boundary."),
        _ => new FileWriteFailed("The file could not be written."),
    };

    private static FileWriteResult OpenFailure(int error) => PosixFileOperations.IsBoundaryViolation(error)
        ? new FileWriteDenied("The target crosses a symbolic link or an inaccessible boundary.")
        : new FileWriteFailed("The file could not be written.");

    /// <summary>Observes the current target through the validated parent descriptor without following a symbolic link.</summary>
    private static async ValueTask<ObservedTarget> ObserveTargetAsync(
        int parent,
        string fileName,
        long maximumFingerprintBytes,
        CancellationToken cancellationToken)
    {
        var descriptor = PosixFileOperations.OpenAt(
            parent,
            fileName,
            _readOnlyFlag | PosixFileOperations.NoFollowFlag | PosixFileOperations.CloseOnExecFlag | PosixFileOperations.NonBlockingFlag,
            0);
        if (descriptor < 0)
        {
            var error = Marshal.GetLastPInvokeError();
            return error == _errorNotFound
                ? ObservedTarget.Absent
                : ObservedTarget.Refused(OpenFailure(error));
        }

        var handle = new SafeFileHandle(new IntPtr(descriptor), ownsHandle: true);
        await using var stream = new FileStream(handle, FileAccess.Read, bufferSize: 81920, isAsync: false);
        if (!stream.CanSeek)
        {
            return ObservedTarget.Refused(new FileWriteFailed("The target is not a regular seekable file."));
        }

        var length = stream.Length;
        if (length > maximumFingerprintBytes)
        {
            return ObservedTarget.Present(length, fingerprint: null);
        }

        using var content = new MemoryStream(capacity: checked((int) length));
        await stream.CopyToAsync(content, cancellationToken).ConfigureAwait(false);
        return ObservedTarget.Present(length, FileSecurityBinding.ContentFingerprint(content.GetBuffer().AsSpan(0, checked((int) content.Length))));
    }

    private static async ValueTask<FileWriteResult> CreateAsync(
        AuthorizedFileWrite operation,
        int parent,
        string fileName,
        ReadOnlyMemory<byte> payload,
        ContentHash payloadFingerprint,
        CancellationToken cancellationToken)
    {
        var descriptor = PosixFileOperations.OpenAt(
            parent,
            fileName,
            PosixFileOperations.WriteOnlyFlag | PosixFileOperations.CreateFlag | PosixFileOperations.ExclusiveFlag | PosixFileOperations.NoFollowFlag | PosixFileOperations.CloseOnExecFlag,
            _newFileCreationMode);
        if (descriptor < 0)
        {
            var error = Marshal.GetLastPInvokeError();
            return error == _errorAlreadyExists
                ? new FileWriteConflict(operation.ResolvedTarget, "The target already exists.")
                : OpenFailure(error);
        }

        var created = false;
        try
        {
            using var handle = new SafeFileHandle(new IntPtr(descriptor), ownsHandle: true);
            created = true;
            _ = PosixFileOperations.ChangeMode(descriptor, _ownerReadWriteMode);
            await using var stream = new FileStream(handle, FileAccess.Write, bufferSize: 81920, isAsync: false);
            await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            created = false;
            return new FileWriteSuccess(
                FileWriteOutcomeKind.Created,
                payload.Length,
                previousBytes: 0,
                finalBytes: payload.Length,
                payloadFingerprint,
                payloadFingerprint);
        }
        finally
        {
            if (created)
            {
                // A create that did not complete must not leave a partial file the caller never learned about.
                _ = PosixFileOperations.UnlinkAt(parent, fileName, 0);
            }
        }
    }

    private static async ValueTask<FileWriteResult> CreateOrReplaceAsync(
        AuthorizedFileWrite operation,
        int parent,
        string fileName,
        ReadOnlyMemory<byte> payload,
        ContentHash payloadFingerprint,
        long observedLength,
        SecurityEnforcementIntentId intentId,
        CancellationToken cancellationToken)
    {
        // The target may be created or removed by another writer between observation and effect, so the whole attempt
        // retries a bounded number of times instead of surfacing a transient race as a failure.
        for (var attempt = 0; attempt < _writeAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var created = await CreateAsync(operation, parent, fileName, payload, payloadFingerprint, cancellationToken).ConfigureAwait(false);
            if (created is not FileWriteConflict)
            {
                return created;
            }

            var replaced = await ReplaceAsync(operation, parent, fileName, payload, payloadFingerprint, observedLength, intentId, cancellationToken).ConfigureAwait(false);
            if (replaced is not FileWriteNotFound)
            {
                return replaced;
            }
        }

        return new FileWriteFailed("The file could not be created or replaced after repeated concurrent modification.");
    }

    /// <summary>Atomically replaces an existing target through a staged write and a descriptor-relative rename.</summary>
    private static async ValueTask<FileWriteResult> ReplaceAsync(
        AuthorizedFileWrite operation,
        int parent,
        string fileName,
        ReadOnlyMemory<byte> payload,
        ContentHash payloadFingerprint,
        long previousBytes,
        SecurityEnforcementIntentId intentId,
        CancellationToken cancellationToken)
    {
        var currentDescriptor = PosixFileOperations.OpenAt(
            parent,
            fileName,
            _readOnlyFlag | PosixFileOperations.NoFollowFlag | PosixFileOperations.CloseOnExecFlag | PosixFileOperations.NonBlockingFlag,
            0);
        if (currentDescriptor < 0)
        {
            var openError = Marshal.GetLastPInvokeError();
            return openError == _errorNotFound ? new FileWriteNotFound(operation.ResolvedTarget) : OpenFailure(openError);
        }

        int mode;
        using (var current = new SafeFileHandle(new IntPtr(currentDescriptor), ownsHandle: true))
        {
            if (!PosixFileOperations.TryGetFileMode(currentDescriptor, out mode))
            {
                return new FileWriteFailed("The target mode could not be observed.");
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        var stagingName = $".agentkit-write-{intentId.Value:N}.tmp";
        var stagingDescriptor = PosixFileOperations.OpenAt(
            parent,
            stagingName,
            PosixFileOperations.WriteOnlyFlag | PosixFileOperations.CreateFlag | PosixFileOperations.ExclusiveFlag | PosixFileOperations.NoFollowFlag | PosixFileOperations.CloseOnExecFlag,
            _ownerReadWriteMode);
        if (stagingDescriptor < 0)
        {
            return new FileWriteFailed("The replacement could not be staged.");
        }

        var stagingExists = true;
        try
        {
            using (var stagingHandle = new SafeFileHandle(new IntPtr(stagingDescriptor), ownsHandle: true))
            await using (var stream = new FileStream(stagingHandle, FileAccess.Write, bufferSize: 81920, isAsync: false))
            {
                if (PosixFileOperations.ChangeMode(stagingDescriptor, mode & _permissionBitsMask) < 0)
                {
                    return new FileWriteFailed("The target mode could not be preserved.");
                }

                await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            cancellationToken.ThrowIfCancellationRequested();
            var stillExists = PosixFileOperations.OpenAt(
                parent,
                fileName,
                _readOnlyFlag | PosixFileOperations.NoFollowFlag | PosixFileOperations.CloseOnExecFlag | PosixFileOperations.NonBlockingFlag,
                0);
            if (stillExists < 0)
            {
                return new FileWriteNotFound(operation.ResolvedTarget);
            }

            _ = PosixFileOperations.CloseDescriptor(stillExists);
            if (PosixFileOperations.RenameAt(parent, stagingName, parent, fileName) < 0)
            {
                return new FileWriteFailed("The staged replacement could not be committed.");
            }

            stagingExists = false;
            return new FileWriteSuccess(
                FileWriteOutcomeKind.Replaced,
                payload.Length,
                previousBytes,
                payload.Length,
                payloadFingerprint,
                payloadFingerprint);
        }
        finally
        {
            if (stagingExists)
            {
                _ = PosixFileOperations.UnlinkAt(parent, stagingName, 0);
            }
        }
    }

    private static async ValueTask<FileWriteResult> AppendAsync(
        AuthorizedFileWrite operation,
        int parent,
        string fileName,
        ReadOnlyMemory<byte> payload,
        ContentHash payloadFingerprint,
        long previousBytes,
        OperatingSystemFileSystemOptionsSnapshot options,
        CancellationToken cancellationToken)
    {
        var finalBytes = previousBytes + payload.Length;
        if (finalBytes > options.Bounds.MaximumWriteBytes)
        {
            return new FileWriteLimitExceeded(options.Bounds.MaximumWriteBytes, finalBytes);
        }

        var descriptor = PosixFileOperations.OpenAt(
            parent,
            fileName,
            PosixFileOperations.WriteOnlyFlag | PosixFileOperations.AppendFlag | PosixFileOperations.NoFollowFlag | PosixFileOperations.CloseOnExecFlag | PosixFileOperations.NonBlockingFlag,
            0);
        if (descriptor < 0)
        {
            var error = Marshal.GetLastPInvokeError();
            return error == _errorNotFound ? new FileWriteNotFound(operation.ResolvedTarget) : OpenFailure(error);
        }

        long finalBytesOnDisk;
        using (var handle = new SafeFileHandle(new IntPtr(descriptor), ownsHandle: true))
        await using (var stream = new FileStream(handle, FileAccess.Write, bufferSize: 81920, isAsync: false))
        {
            if (!stream.CanSeek)
            {
                return new FileWriteFailed("The target is not a regular seekable file.");
            }

            // The stream positional-writes at its own offset, which a fresh descriptor reports as zero; some platforms honor that
            // offset even with O_APPEND, so position explicitly at the end instead of overwriting the head of the file.
            _ = stream.Seek(0, SeekOrigin.End);
            await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            finalBytesOnDisk = RandomAccess.GetLength(handle);
        }

        var finalFingerprint = await FingerprintAsync(parent, fileName, cancellationToken).ConfigureAwait(false);
        return finalFingerprint is null
            ? new FileWriteFailed("The appended file could not be fingerprinted.")
            : new FileWriteSuccess(
                FileWriteOutcomeKind.Appended,
                payload.Length,
                previousBytes,
                finalBytesOnDisk,
                payloadFingerprint,
                finalFingerprint.Value);
    }

    private static async ValueTask<ContentHash?> FingerprintAsync(int parent, string fileName, CancellationToken cancellationToken)
    {
        var descriptor = PosixFileOperations.OpenAt(
            parent,
            fileName,
            _readOnlyFlag | PosixFileOperations.NoFollowFlag | PosixFileOperations.CloseOnExecFlag | PosixFileOperations.NonBlockingFlag,
            0);
        if (descriptor < 0)
        {
            return null;
        }

        var handle = new SafeFileHandle(new IntPtr(descriptor), ownsHandle: true);
        await using var stream = new FileStream(handle, FileAccess.Read, bufferSize: 81920, isAsync: false);
        using var content = new MemoryStream();
        await stream.CopyToAsync(content, cancellationToken).ConfigureAwait(false);
        return FileSecurityBinding.ContentFingerprint(content.GetBuffer().AsSpan(0, checked((int) content.Length)));
    }

    /// <summary>The observed state of a write target before any effect.</summary>
    /// <param name="Exists">Whether a regular file exists.</param>
    /// <param name="Length">The file length in bytes; zero when absent.</param>
    /// <param name="Fingerprint">The content fingerprint when the file was small enough to read; otherwise null.</param>
    /// <param name="Failure">A terminal refusal when the target could not be observed safely; otherwise null.</param>
    private sealed record ObservedTarget(bool Exists, long Length, ContentHash? Fingerprint, FileWriteResult? Failure)
    {
        internal static ObservedTarget Absent { get; } = new(false, 0, null, null);

        internal static ObservedTarget Present(long length, ContentHash? fingerprint) => new(true, length, fingerprint, null);

        internal static ObservedTarget Refused(FileWriteResult failure) => new(false, 0, null, failure);
    }
}
