// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem.InMemory.Tests;

using AgentKit.Conformance;

using Microsoft.Extensions.Options;

/// <summary>Conformance fixture for the in-memory file-system profile.</summary>
public sealed class InMemoryFileSystemConformanceFixture: IFileSystemConformanceFixture
{
    private ISecurityAuditDispatcher _audit = new AcceptingAuditDispatcher();
    private InMemoryFileSystem _fileSystem;

    /// <summary>Initializes a fresh in-memory volume with host capabilities.</summary>
    public InMemoryFileSystemConformanceFixture() => _fileSystem = CreateFileSystem();

    /// <inheritdoc/>
    public bool SupportsSymlinkRejection => false;

    /// <inheritdoc/>
    public IFileReader Reader => _fileSystem;

    /// <inheritdoc/>
    public IFileWriter Writer => _fileSystem;

    /// <inheritdoc/>
    public IDirectoryCreator? DirectoryCreator => _fileSystem;

    /// <inheritdoc/>
    public ISecurityGrantStore GrantStore { get; } = TestSecurity.GrantStore();

    /// <inheritdoc/>
    public async ValueTask SeedFileAsync(string relativePath, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default)
    {
        var operation = CreateAuthorizedWrite(relativePath, content, FileWriteDisposition.CreateOrReplace);
        _ = await _fileSystem.WriteAsync(
            operation,
            new FileWriteContent(content, FileSecurityBinding.ContentFingerprint(content.Span)),
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public AuthorizedFileRead CreateAuthorizedRead(string relativePath, long maxBytes)
    {
        var target = new FileTarget(new FileRootId("workspace"), new NormalizedRelativePath(relativePath));
        var request = new FileReadRequest(
            new FileOperationId(Guid.NewGuid()),
            new OperationId(Guid.NewGuid()),
            new AgentId(Guid.NewGuid()),
            runId: null,
            target,
            new FileReadBounds(maxBytes));
        var resolved = new ResolvedFileTarget(
            new FileRootId("workspace"),
            new NormalizedRelativePath(relativePath),
            hostTargetPath: relativePath,
            FilePathComparisonKind.Ordinal,
            FileSecurityBinding.ContentFingerprint("no-link"u8),
            FileSecurityBinding.ContentFingerprint("target"u8));
        return new AuthorizedFileRead(request, resolved, TestSecurity.Grant());
    }

    /// <inheritdoc/>
    public AuthorizedFileWrite CreateAuthorizedWrite(
        string relativePath,
        ReadOnlyMemory<byte> payload,
        FileWriteDisposition disposition,
        ContentHash? expectedTargetFingerprint = null)
    {
        var payloadFingerprint = FileSecurityBinding.ContentFingerprint(payload.Span);
        var resolved = new ResolvedFileTarget(
            new FileRootId("workspace"),
            new NormalizedRelativePath(relativePath),
            hostTargetPath: relativePath,
            FilePathComparisonKind.Ordinal,
            FileSecurityBinding.ContentFingerprint("no-link"u8),
            expectedTargetFingerprint ?? FileSecurityBinding.ContentFingerprint("target"u8));
        return new AuthorizedFileWrite(
            resolved,
            disposition,
            expectedTargetFingerprint,
            payload.Length,
            payloadFingerprint,
            FileWriteAtomicityMode.Required,
            FileWriteEffectClass.WorkspaceBytes,
            TestSecurity.Grant());
    }

    /// <inheritdoc/>
    public AuthorizedDirectoryCreate CreateAuthorizedDirectoryCreate(string relativePath)
    {
        var resolved = new ResolvedFileTarget(
            new FileRootId("workspace"),
            new NormalizedRelativePath(relativePath),
            hostTargetPath: relativePath,
            FilePathComparisonKind.Ordinal,
            FileSecurityBinding.ContentFingerprint("no-link"u8),
            FileSecurityBinding.ContentFingerprint("target"u8));
        return new AuthorizedDirectoryCreate(resolved, TestSecurity.Grant());
    }

    /// <inheritdoc/>
    public void UseRejectingAudit()
    {
        _audit = new RejectingAuditDispatcher();
        _fileSystem = CreateFileSystem();
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private InMemoryFileSystem CreateFileSystem() => new(
        Options.Create(new InMemoryFileSystemOptions()),
        GrantStore,
        TimeProvider.System,
        logger: null,
        new GuidSecurityEnforcementIntentIdGenerator(),
        _audit,
        new GuidSecurityAuditRecordIdGenerator(),
        new FileSystemProfileKey("conformance"));

    private sealed class AcceptingAuditDispatcher: ISecurityAuditDispatcher
    {
        public ValueTask<SecurityAuditDispatchResult> DispatchAsync(
            SecurityAuditRecord record,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<SecurityAuditDispatchResult>(new SecurityAuditAccepted());
    }

    private sealed class RejectingAuditDispatcher: ISecurityAuditDispatcher
    {
        public ValueTask<SecurityAuditDispatchResult> DispatchAsync(
            SecurityAuditRecord record,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<SecurityAuditDispatchResult>(new SecurityAuditUnavailable("No sink."));
    }
}
