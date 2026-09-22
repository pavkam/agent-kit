// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem;

/// <summary>Grant, audit, and host commit orchestration for operating-system file writes.</summary>
internal static class OperatingSystemFileWriteOperations
{
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
        if (root is null || !PosixFileOperations.IsUnderRoot(operation.ResolvedTarget.HostTargetPath, root.HostRootPath))
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

        var hostPath = operation.ResolvedTarget.HostTargetPath;
        var exists = File.Exists(hostPath);
        long previousBytes = 0;
        ContentHash? currentFingerprint = null;
        if (exists)
        {
            var probe = PosixFileOperations.TryOpenRegularFileReadOnly(hostPath);
            if (probe.Status is PosixFileOperations.PosixOpenReadStatus.Success)
            {
                previousBytes = probe.Length;
                if (previousBytes <= options.Bounds.MaximumReadBytes)
                {
                    try
                    {
                        var bytes = await File.ReadAllBytesAsync(hostPath, cancellationToken).ConfigureAwait(false);
                        currentFingerprint = FileSecurityBinding.ContentFingerprint(bytes);
                    }
                    catch (IOException)
                    {
                        return new FileWriteFailed("The target fingerprint could not be observed.");
                    }
                }
            }
        }

        return operation.ExpectedTargetFingerprint is { } expected
            && currentFingerprint is { } observed
            && observed != expected
            ? new FileWriteConflict(
                operation.ResolvedTarget,
                "The target fingerprint did not match the required precondition.")
            : operation.ExpectedTargetFingerprint is not null && !exists
            ? new FileWriteNotFound(operation.ResolvedTarget)
            : operation.Disposition switch
            {
                FileWriteDisposition.CreateOnly when exists => new FileWriteConflict(
                    operation.ResolvedTarget,
                    "The target already exists."),
                FileWriteDisposition.CreateOnly => await CommitNewAsync(
                    operation.ResolvedTarget,
                    hostPath,
                    payload,
                    actualPayloadFingerprint,
                    cancellationToken),
                FileWriteDisposition.ReplaceExisting when !exists => new FileWriteNotFound(operation.ResolvedTarget),
                FileWriteDisposition.ReplaceExisting => await CommitReplaceAsync(
                    hostPath,
                    payload,
                    actualPayloadFingerprint,
                    previousBytes,
                    intent.Id,
                    cancellationToken),
                FileWriteDisposition.CreateOrReplace when !exists => await CommitNewAsync(
                    operation.ResolvedTarget,
                    hostPath,
                    payload,
                    actualPayloadFingerprint,
                    cancellationToken),
                FileWriteDisposition.CreateOrReplace => await CommitReplaceAsync(
                    hostPath,
                    payload,
                    actualPayloadFingerprint,
                    previousBytes,
                    intent.Id,
                    cancellationToken),
                FileWriteDisposition.Append when !exists => new FileWriteNotFound(operation.ResolvedTarget),
                FileWriteDisposition.Append => await CommitAppendAsync(
                    hostPath,
                    payload,
                    actualPayloadFingerprint,
                    previousBytes,
                    options,
                    cancellationToken),
                _ => new FileWriteFailed("The write disposition is not supported."),
            };
    }

    private static async ValueTask<FileWriteResult> CommitNewAsync(
        ResolvedFileTarget _,
        string hostPath,
        ReadOnlyMemory<byte> payload,
        ContentHash payloadFingerprint,
        CancellationToken cancellationToken)
    {
        try
        {
            var directory = Path.GetDirectoryName(hostPath);
            if (directory is not null && !Directory.Exists(directory))
            {
                return new FileWriteFailed("A parent directory does not exist.");
            }

            await using var stream = new FileStream(
                hostPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                FileOptions.Asynchronous);
            await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            var finalFingerprint = FileSecurityBinding.ContentFingerprint(payload.Span);
            return new FileWriteSuccess(
                FileWriteOutcomeKind.Created,
                payload.Length,
                previousBytes: 0,
                finalBytes: payload.Length,
                payloadFingerprint,
                finalFingerprint);
        }
        catch (IOException)
        {
            return new FileWriteFailed("The file could not be created.");
        }
    }

    private static async ValueTask<FileWriteResult> CommitReplaceAsync(
        string hostPath,
        ReadOnlyMemory<byte> payload,
        ContentHash payloadFingerprint,
        long previousBytes,
        SecurityEnforcementIntentId intentId,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(hostPath)
            ?? throw new InvalidOperationException("Host target path must include a directory.");
        var stagingPath = Path.Combine(directory, $".agentkit-write-{intentId.Value:N}.tmp");
        try
        {
            await File.WriteAllBytesAsync(stagingPath, payload, cancellationToken).ConfigureAwait(false);
            File.Move(stagingPath, hostPath, overwrite: true);
            var finalFingerprint = FileSecurityBinding.ContentFingerprint(payload.Span);
            return new FileWriteSuccess(
                FileWriteOutcomeKind.Replaced,
                payload.Length,
                previousBytes,
                payload.Length,
                payloadFingerprint,
                finalFingerprint);
        }
        catch (IOException)
        {
            TryDelete(stagingPath);
            return new FileWriteFailed("The file could not be replaced.");
        }
        catch (OperationCanceledException)
        {
            TryDelete(stagingPath);
            throw;
        }
    }

    private static async ValueTask<FileWriteResult> CommitAppendAsync(
        string hostPath,
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

        try
        {
            long finalBytesOnDisk;
            await using (var stream = new FileStream(
                hostPath,
                FileMode.Open,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                FileOptions.Asynchronous))
            {
                _ = stream.Seek(0, SeekOrigin.End);
                await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                finalBytesOnDisk = stream.Length;
            }

            var allBytes = await File.ReadAllBytesAsync(hostPath, cancellationToken).ConfigureAwait(false);
            var finalFingerprint = FileSecurityBinding.ContentFingerprint(allBytes);
            return new FileWriteSuccess(
                FileWriteOutcomeKind.Appended,
                payload.Length,
                previousBytes,
                finalBytesOnDisk,
                payloadFingerprint,
                finalFingerprint);
        }
        catch (IOException)
        {
            return new FileWriteFailed("The file could not be appended.");
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
