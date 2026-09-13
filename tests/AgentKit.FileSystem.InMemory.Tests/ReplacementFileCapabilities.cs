// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem.InMemory.Tests;

/// <summary>Provides distinguishable replacements for each narrow filesystem capability without performing host effects.</summary>
internal sealed class ReplacementFileCapabilities:
    IDirectoryReader,
    IFileGlobber,
    IFileContentSearcher,
    IFileSnapshotReader,
    IAtomicFileReplacer,
    IWorkspacePatchApplier
{
    /// <inheritdoc/>
    public ComponentId SecurityAudience { get; } = new("replacement-file-capability");

    /// <inheritdoc/>
    public ValueTask<DirectoryEnumerationResult> EnumerateAsync(
        DirectoryEnumerationRequest request,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public ValueTask<GlobResult> GlobAsync(GlobRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    /// <inheritdoc/>
    public ValueTask<FileSearchResult> SearchAsync(
        FileSearchRequest request,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public ValueTask<FileSnapshotResult> ReadSnapshotAsync(
        FileSnapshotRequest request,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public ValueTask<AtomicFileReplaceResult> ReplaceAsync(
        AtomicFileReplaceRequest request,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public ValueTask<WorkspacePatchResult> ApplyPatchAsync(
        WorkspacePatchRequest request,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
