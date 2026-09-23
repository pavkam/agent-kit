// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

/// <summary>Keyed sandbox host exposing legacy narrow file capabilities for one OS profile.</summary>
internal sealed class OperatingSystemFileSystemLegacyHost:
    ILegacyDirectoryReader,
    IFileGlobber,
    IFileContentSearcher,
    IFileSnapshotReader,
    IAtomicFileReplacer,
    IWorkspacePatchApplier
{
    private readonly SandboxedFileSystem _inner;

    /// <summary>Creates a legacy host bound to one profile snapshot.</summary>
    /// <param name="provider">The root provider.</param>
    /// <param name="snapshot">The immutable profile snapshot.</param>
    /// <exception cref="InvalidOperationException">The snapshot declares no roots.</exception>
    public OperatingSystemFileSystemLegacyHost(IServiceProvider provider, OperatingSystemFileSystemOptionsSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.Roots.IsDefaultOrEmpty)
        {
            throw new InvalidOperationException("The operating-system profile must declare at least one root.");
        }

        var root = snapshot.Roots[0].HostRootPath;
        var options = Options.Create(new SandboxedFileSystemOptions
        {
            RootDirectory = root,
            MaximumReadBytes = snapshot.Bounds.MaximumReadBytes,
            MaximumWriteBytes = snapshot.Bounds.MaximumWriteBytes,
        });
        _inner = new SandboxedFileSystem(
            options,
            provider.GetRequiredService<ISecurityGrantStore>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetService<ILogger<SandboxedFileSystem>>(),
            provider.GetRequiredService<IIdentifierGenerator<SecurityEnforcementIntentId>>());
    }

    /// <inheritdoc/>
    public ComponentId SecurityAudience => _inner.SecurityAudience;

    /// <inheritdoc/>
    public ValueTask<DirectoryEnumerationResult> EnumerateAsync(
        DirectoryEnumerationRequest request,
        CancellationToken cancellationToken = default) =>
        _inner.EnumerateAsync(request, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<GlobResult> GlobAsync(GlobRequest request, CancellationToken cancellationToken = default) =>
        _inner.GlobAsync(request, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<FileSearchResult> SearchAsync(FileSearchRequest request, CancellationToken cancellationToken = default) =>
        _inner.SearchAsync(request, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<FileSnapshotResult> ReadSnapshotAsync(FileSnapshotRequest request, CancellationToken cancellationToken = default) =>
        _inner.ReadSnapshotAsync(request, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<AtomicFileReplaceResult> ReplaceAsync(AtomicFileReplaceRequest request, CancellationToken cancellationToken = default) =>
        _inner.ReplaceAsync(request, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<WorkspacePatchResult> ApplyPatchAsync(WorkspacePatchRequest request, CancellationToken cancellationToken = default) =>
        _inner.ApplyPatchAsync(request, cancellationToken);
}
