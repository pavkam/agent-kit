// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem.InMemory;

/// <summary>Spec host capability contracts for the in-memory virtual tree.</summary>
public sealed partial class InMemoryFileSystem
{
    /// <inheritdoc/>
    public async ValueTask<FileReadOpenResult> OpenReadAsync(
        AuthorizedFileRead operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (_hostAuditDispatcher is null || _hostAuditRecordIds is null)
        {
            return new FileReadOpenDenied("Host audit is not configured for this in-memory volume.");
        }

        var path = VirtualPath(operation.ResolvedTarget);
        var enforcement = FileSystemEnforcementReceipt.Create(
            operation.Grant,
            SecurityAudience,
            SecurityOperationKind.FileRead,
            SecurityEffect.Observe,
            [FileSecurityBinding.Resource(operation.ResolvedTarget)],
            FileSecurityBinding.ReadFingerprint(operation.Request));
        var intent = new SecurityEnforcementIntent(_intentIds.Create(), null);
        var denial = await InMemoryFileSystemHostGuard.ConsumeWithRequiredAuditAsync(
            operation.Grant,
            enforcement,
            intent,
            _grantStore,
            _hostAuditDispatcher,
            _hostAuditRecordIds,
            _timeProvider,
            cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (denial is not null)
        {
            return new FileReadOpenDenied(denial);
        }

        ImmutableArray<byte> content;
        lock (_gate)
        {
            if (!_files.TryGetValue(path, out content))
            {
                return new FileReadOpenNotFound();
            }
        }

        var effectiveMax = Math.Min(operation.Request.Bounds.MaxBytes, _maximumReadBytes);
        if (content.Length > effectiveMax)
        {
            content = content[..(int) effectiveMax];
        }

        var metadata = new FileMetadata(content.Length, lastModifiedUtc: _timeProvider.GetUtcNow(), contentFingerprint: null);
        var stream = new MemoryStream([.. content], writable: false);
        return new FileReadHandleOpened(new InMemoryFileReadHandle(metadata, stream));
    }

    /// <inheritdoc/>
    public async ValueTask<FileWriteResult> WriteAsync(
        AuthorizedFileWrite operation,
        FileWriteContent content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(content);
        if (_hostAuditDispatcher is null || _hostAuditRecordIds is null)
        {
            return new FileWriteDenied("Host audit is not configured for this in-memory volume.");
        }

        var payload = content.Payload;
        var payloadFingerprint = FileSecurityBinding.ContentFingerprint(payload.Span);
        if (payloadFingerprint != content.PayloadFingerprint
            || payload.Length != operation.DeclaredContentLength
            || payloadFingerprint != operation.DeclaredContentFingerprint)
        {
            return new FileWriteFailed("The payload did not match the declared write evidence.");
        }

        if (payload.Length > _maximumWriteBytes)
        {
            return new FileWriteLimitExceeded(_maximumWriteBytes, payload.Length);
        }

        var path = VirtualPath(operation.ResolvedTarget);
        var enforcement = FileSystemEnforcementReceipt.Create(
            operation.Grant,
            SecurityAudience,
            SecurityOperationKind.FileWrite,
            FileSecurityBinding.WriteEffect(operation.Disposition),
            [FileSecurityBinding.Resource(operation.ResolvedTarget)],
            FileSecurityBinding.WriteFingerprint(operation, payloadFingerprint));
        var intent = new SecurityEnforcementIntent(_intentIds.Create(), null);
        var denial = await InMemoryFileSystemHostGuard.ConsumeWithRequiredAuditAsync(
            operation.Grant,
            enforcement,
            intent,
            _grantStore,
            _hostAuditDispatcher,
            _hostAuditRecordIds,
            _timeProvider,
            cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (denial is not null)
        {
            return new FileWriteDenied(denial);
        }

        lock (_gate)
        {
            var exists = _files.ContainsKey(path);
            ContentHash? currentFingerprint = exists
                ? FileSecurityBinding.ContentFingerprint(_files[path].AsSpan())
                : null;
            return operation.ExpectedTargetFingerprint is { } expected
                && currentFingerprint is { } observed
                && observed != expected
                ? new FileWriteConflict(operation.ResolvedTarget, "The target fingerprint did not match the required precondition.")
                : operation.ExpectedTargetFingerprint is not null && !exists
                ? new FileWriteNotFound(operation.ResolvedTarget)
                : operation.Disposition switch
                {
                    FileWriteDisposition.CreateOnly when exists => new FileWriteConflict(
                        operation.ResolvedTarget,
                        "The target already exists."),
                    FileWriteDisposition.ReplaceExisting when !exists => new FileWriteNotFound(operation.ResolvedTarget),
                    FileWriteDisposition.Append when !exists => new FileWriteNotFound(operation.ResolvedTarget),
                    FileWriteDisposition.CreateOnly or FileWriteDisposition.CreateOrReplace when !exists => CommitCreate(
                        path,
                        payload,
                        payloadFingerprint),
                    FileWriteDisposition.ReplaceExisting or FileWriteDisposition.CreateOrReplace => CommitReplace(
                        path,
                        payload,
                        payloadFingerprint,
                        exists ? _files[path].Length : 0),
                    FileWriteDisposition.Append => CommitAppend(path, payload, payloadFingerprint),
                    _ => new FileWriteFailed("The write disposition is not supported."),
                };
        }
    }

    /// <inheritdoc/>
    public async ValueTask<FileMetadataResult> GetMetadataAsync(
        AuthorizedFileMetadataRead operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (_hostAuditDispatcher is null || _hostAuditRecordIds is null)
        {
            return new FileMetadataDenied("Host audit is not configured for this in-memory volume.");
        }

        var path = VirtualPath(operation.ResolvedTarget);
        var fsPath = new FileSystemPath(VirtualPath(operation.ResolvedTarget));
        var enforcement = FileSystemEnforcementReceipt.Create(
            operation.Grant,
            SecurityAudience,
            SecurityOperationKind.FileRead,
            SecurityEffect.Observe,
            [FileSecurityBinding.Resource(operation.ResolvedTarget)],
            FileSecurityBinding.ReadFingerprint(fsPath));
        var intent = new SecurityEnforcementIntent(_intentIds.Create(), null);
        var denial = await InMemoryFileSystemHostGuard.ConsumeWithRequiredAuditAsync(
            operation.Grant,
            enforcement,
            intent,
            _grantStore,
            _hostAuditDispatcher,
            _hostAuditRecordIds,
            _timeProvider,
            cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (denial is not null)
        {
            return new FileMetadataDenied(denial);
        }

        lock (_gate)
        {
            return _files.TryGetValue(path, out var content)
                ? new FileMetadataSuccess(new FileMetadata(
                    content.Length,
                    _timeProvider.GetUtcNow(),
                    FileSecurityBinding.ContentFingerprint(content.AsSpan())))
                : new FileMetadataNotFound(operation.ResolvedTarget);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<DirectoryCreateResult> CreateAsync(
        AuthorizedDirectoryCreate operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (_hostAuditDispatcher is null || _hostAuditRecordIds is null)
        {
            return new DirectoryCreateDenied("Host audit is not configured for this in-memory volume.");
        }

        var path = operation.ResolvedTarget.RelativePath.Value;
        var fsPath = new FileSystemPath(path);
        var enforcement = FileSystemEnforcementReceipt.Create(
            operation.Grant,
            SecurityAudience,
            SecurityOperationKind.FileWrite,
            SecurityEffect.Create,
            [FileSecurityBinding.Resource(new FileTarget(operation.ResolvedTarget.RootId, operation.ResolvedTarget.RelativePath))],
            FileSecurityBinding.ReadFingerprint(fsPath));
        var intent = new SecurityEnforcementIntent(_intentIds.Create(), null);
        var denial = await InMemoryFileSystemHostGuard.ConsumeWithRequiredAuditAsync(
            operation.Grant,
            enforcement,
            intent,
            _grantStore,
            _hostAuditDispatcher,
            _hostAuditRecordIds,
            _timeProvider,
            cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (denial is not null)
        {
            return new DirectoryCreateDenied(denial);
        }

        lock (_gate)
        {
            if (_directories.Contains(path) || _files.ContainsKey(path))
            {
                return new DirectoryCreateAlreadyExists(operation.ResolvedTarget);
            }

            _ = _directories.Add(path);
            return new DirectoryCreateSuccess(operation.ResolvedTarget);
        }
    }

    private static string VirtualPath(ResolvedFileTarget target) => target.RelativePath.Value;

    private FileWriteSuccess CommitCreate(string path, ReadOnlyMemory<byte> payload, ContentHash payloadFingerprint)
    {
        _files[path] = payload.ToArray().ToImmutableArray();
        var finalFingerprint = FileSecurityBinding.ContentFingerprint(payload.Span);
        return new FileWriteSuccess(
            FileWriteOutcomeKind.Created,
            payload.Length,
            0,
            payload.Length,
            payloadFingerprint,
            finalFingerprint);
    }

    private FileWriteSuccess CommitReplace(string path, ReadOnlyMemory<byte> payload, ContentHash payloadFingerprint, long previousBytes)
    {
        _files[path] = payload.ToArray().ToImmutableArray();
        var finalFingerprint = FileSecurityBinding.ContentFingerprint(payload.Span);
        return new FileWriteSuccess(
            FileWriteOutcomeKind.Replaced,
            payload.Length,
            previousBytes,
            payload.Length,
            payloadFingerprint,
            finalFingerprint);
    }

    private FileWriteResult CommitAppend(string path, ReadOnlyMemory<byte> payload, ContentHash payloadFingerprint)
    {
        var existing = _files[path];
        var combined = existing.AddRange(payload.ToArray());
        if (combined.Length > _maximumWriteBytes)
        {
            return new FileWriteLimitExceeded(_maximumWriteBytes, combined.Length);
        }

        _files[path] = combined;
        var finalFingerprint = FileSecurityBinding.ContentFingerprint(combined.AsSpan());
        return new FileWriteSuccess(
            FileWriteOutcomeKind.Appended,
            payload.Length,
            existing.Length,
            combined.Length,
            payloadFingerprint,
            finalFingerprint);
    }

    private sealed class InMemoryFileReadHandle(FileMetadata metadata, Stream content): IFileReadHandle
    {
        public FileMetadata Metadata { get; } = metadata;

        public Stream Content { get; } = content;

        public ValueTask DisposeAsync()
        {
            Content.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
