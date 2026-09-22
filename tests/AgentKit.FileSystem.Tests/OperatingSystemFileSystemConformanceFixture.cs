// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem.Tests;

using AgentKit.Conformance;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Conformance fixture for the operating-system file-system profile.</summary>
public sealed class OperatingSystemFileSystemConformanceFixture: IFileSystemConformanceFixture
{
    private readonly string _root;
    private ISecurityAuditDispatcher _audit = new AcceptingAuditDispatcher();
    private ServiceProvider? _provider;

    /// <summary>Initializes a new isolated temporary root.</summary>
    public OperatingSystemFileSystemConformanceFixture()
    {
        _root = Path.Combine(Path.GetTempPath(), $"agentkit-fs-conformance-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(_root);
        RebuildProvider();
    }

    /// <inheritdoc/>
    public bool SupportsSymlinkRejection => PosixFileOperations.IsSecureTraversalSupported;

    /// <inheritdoc/>
    public IFileReader Reader => Provider.GetRequiredKeyedService<IFileReader>("conformance");

    /// <inheritdoc/>
    public IFileWriter Writer => Provider.GetRequiredKeyedService<IFileWriter>("conformance");

    /// <inheritdoc/>
    public IDirectoryCreator? DirectoryCreator => null;

    /// <inheritdoc/>
    public ISecurityGrantStore GrantStore => Provider.GetRequiredService<ISecurityGrantStore>();

    private ServiceProvider Provider => _provider ?? throw new InvalidOperationException("The fixture provider is not initialized.");

    /// <inheritdoc/>
    public async ValueTask SeedFileAsync(string relativePath, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(_root, relativePath);
        var directory = Path.GetDirectoryName(path);
        if (directory is not null)
        {
            _ = Directory.CreateDirectory(directory);
        }

        await File.WriteAllBytesAsync(path, content.ToArray(), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public AuthorizedFileRead CreateAuthorizedRead(string relativePath, long maxBytes)
    {
        var hostTarget = Path.GetFullPath(Path.Combine(_root, relativePath));
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
            hostTarget,
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
        var hostTarget = Path.GetFullPath(Path.Combine(_root, relativePath));
        var payloadFingerprint = FileSecurityBinding.ContentFingerprint(payload.Span);
        var resolved = new ResolvedFileTarget(
            new FileRootId("workspace"),
            new NormalizedRelativePath(relativePath),
            hostTarget,
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
        var hostTarget = Path.GetFullPath(Path.Combine(_root, relativePath));
        var resolved = new ResolvedFileTarget(
            new FileRootId("workspace"),
            new NormalizedRelativePath(relativePath),
            hostTarget,
            FilePathComparisonKind.Ordinal,
            FileSecurityBinding.ContentFingerprint("no-link"u8),
            FileSecurityBinding.ContentFingerprint("target"u8));
        return new AuthorizedDirectoryCreate(resolved, TestSecurity.Grant());
    }

    /// <inheritdoc/>
    public void UseRejectingAudit()
    {
        _audit = new RejectingAuditDispatcher();
        RebuildProvider();
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        _provider?.Dispose();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }

        return ValueTask.CompletedTask;
    }

    private void RebuildProvider()
    {
        _provider?.Dispose();
        var services = new ServiceCollection();
        _ = services.AddSingleton<ISecurityGrantStore>(TestSecurity.GrantStore());
        _ = services.AddSingleton(_audit);
        _ = services.AddOperatingSystemFileSystem(
            new FileSystemProfileKey("conformance"),
            options => options.Roots.Add(new FileRootRegistration(new FileRootId("workspace"), _root)));
        _provider = services.BuildServiceProvider();
    }

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
