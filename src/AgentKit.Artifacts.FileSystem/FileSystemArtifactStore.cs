// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.FileSystem;

/// <summary>Stores artifact entries and payloads through AgentKit's protected file-system contracts.</summary>
/// <remarks>
/// <para>
/// Every artifact operation first consumes its own single-use artifact grant, then performs each file read and write under a separate
/// security request bound to the same captured authorization, so the file boundary enforces its own exact grant for every concrete
/// effect. Writes name an explicit disposition; the store creates no directory, deletes nothing the contracts cannot delete, and
/// never opens a host path itself.
/// </para>
/// <para>
/// Entry state is a flushed newline-delimited log replayed through the same planner the other adapters run, so staging visibility,
/// single-winner finalization, tenant partitioning, replay identity, and tombstones are identical. The store advertises durability
/// only to the extent the selected file-system profile provides it, holds no lock on the root, and claims no multi-process
/// coordination. The instance is thread-safe.
/// </para>
/// </remarks>
public sealed class FileSystemArtifactStore: IArtifactStore, IDisposable
{
    private readonly ArtifactStoreGateway _gateway;
    private readonly FileSystemArtifactBackend _backend;

    /// <summary>Initializes a store bound to one file-system profile and root without performing any effect.</summary>
    /// <param name="target">The non-null profile, logical root, and host root.</param>
    /// <param name="settings">The non-null immutable bounds.</param>
    /// <param name="fileSystems">The selector that resolves the profile's reader and writer.</param>
    /// <param name="authorities">The selector that activates the captured authority for each file effect.</param>
    /// <param name="requestIds">The security request identity source.</param>
    /// <param name="fileOperationIds">The file-operation identity source.</param>
    /// <param name="grants">The authoritative grant store that validates and consumes each artifact grant.</param>
    /// <param name="intentIds">The allocator of fresh enforcement-intent identities.</param>
    /// <param name="time">The clock used for expiry, publication evidence, authorization deadlines, and observational duration.</param>
    /// <param name="logger">The optional content-free logger.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    public FileSystemArtifactStore(
        FileSystemArtifactTarget target,
        FileSystemArtifactSettings settings,
        IFileSystemSelector fileSystems,
        ISecurityAuthoritySelector authorities,
        IIdentifierGenerator<SecurityRequestId> requestIds,
        IIdentifierGenerator<FileOperationId> fileOperationIds,
        ISecurityGrantStore grants,
        IIdentifierGenerator<SecurityEnforcementIntentId> intentIds,
        TimeProvider time,
        ILogger<FileSystemArtifactStore>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(fileSystems);
        ArgumentNullException.ThrowIfNull(authorities);
        ArgumentNullException.ThrowIfNull(requestIds);
        ArgumentNullException.ThrowIfNull(fileOperationIds);
        ArgumentNullException.ThrowIfNull(grants);
        ArgumentNullException.ThrowIfNull(intentIds);
        ArgumentNullException.ThrowIfNull(time);
        _backend = new FileSystemArtifactBackend(
            new FileSystemArtifactEffects(target, settings, fileSystems, authorities, requestIds, fileOperationIds, time), settings, logger);
        _gateway = new ArtifactStoreGateway("file_system", new ComponentId("agentkit.artifacts.file-system"), _backend, grants, intentIds, time, logger);
    }

    /// <inheritdoc/>
    public ComponentId SecurityAudience => _gateway.SecurityAudience;

    /// <inheritdoc/>
    public Task<ArtifactStorePrepareResult> PrepareAsync(ArtifactStorePrepareRequest request, CancellationToken cancellationToken = default) =>
        _gateway.PrepareAsync(request, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<ArtifactStoreFinalizeResult> FinalizeAsync(ArtifactStoreFinalizeRequest request, CancellationToken cancellationToken = default) =>
        _gateway.FinalizeAsync(request, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<ArtifactStoreAbortResult> AbortAsync(ArtifactStoreAbortRequest request, CancellationToken cancellationToken = default) =>
        _gateway.AbortAsync(request, cancellationToken);

    /// <inheritdoc/>
    public Task<ArtifactStoreReadResult> ReadAsync(ArtifactStoreReadRequest request, CancellationToken cancellationToken = default) =>
        _gateway.ReadAsync(request, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<ArtifactStoreDeleteResult> DeleteAsync(ArtifactStoreDeleteRequest request, CancellationToken cancellationToken = default) =>
        _gateway.DeleteAsync(request, cancellationToken);

    /// <summary>Releases the store's operation gate.</summary>
    /// <remarks>Disposal is idempotent. Acknowledged operations were already committed and are unaffected.</remarks>
    public void Dispose() => _backend.Dispose();
}
