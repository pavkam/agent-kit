// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Sqlite;

/// <summary>Stores staged and committed artifact bytes in one host-local, exclusively held SQLite database.</summary>
/// <remarks>
/// <para>
/// Staging visibility, immutable committed bytes, tenant partitioning, replay identity, reference publication, and tombstones are
/// persisted transactionally and survive close and reopen. Every operation consumes a single-use grant that binds that exact
/// operation before any state is read or written; consumed grants are never retained with stored content.
/// </para>
/// <para>
/// The adapter advertises durable local content only: no distributed replication, no cloud-object retention, no fencing across
/// hosts, and no atomic transaction with session history. The database is held exclusively while the store is open. The
/// instance is thread-safe and owns its connection; dispose it to release the file.
/// </para>
/// </remarks>
public sealed class SqliteArtifactStore: IArtifactStore, IDisposable
{
    private readonly ArtifactStoreGateway _gateway;
    private readonly SqliteArtifactBackend _backend;

    /// <summary>Initializes a store bound to one host-authorized database without opening it.</summary>
    /// <param name="target">The non-null exact database and bootstrap effects.</param>
    /// <param name="settings">The non-null immutable lock and size bounds.</param>
    /// <param name="grants">The authoritative grant store that validates and consumes each exact grant.</param>
    /// <param name="intentIds">The allocator of fresh enforcement-intent identities.</param>
    /// <param name="time">The clock used for expiry, publication evidence, and observational duration.</param>
    /// <param name="logger">The optional content-free logger.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    public SqliteArtifactStore(
        SqliteArtifactTarget target,
        SqliteArtifactSettings settings,
        ISecurityGrantStore grants,
        IIdentifierGenerator<SecurityEnforcementIntentId> intentIds,
        TimeProvider time,
        ILogger<SqliteArtifactStore>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(grants);
        ArgumentNullException.ThrowIfNull(intentIds);
        ArgumentNullException.ThrowIfNull(time);
        _backend = new SqliteArtifactBackend(new SqliteArtifactDatabase(target, settings, SqliteArtifactSchema.Store), logger);
        _gateway = new ArtifactStoreGateway("sqlite", new ComponentId("agentkit.artifacts.sqlite"), _backend, grants, intentIds, time, logger);
    }

    /// <inheritdoc/>
    public ComponentId SecurityAudience => _gateway.SecurityAudience;

    /// <summary>Creates or validates the schema, binds the database to its expected identity, and replays every persisted entry.</summary>
    /// <param name="cancellationToken">Cancels before initialization completes.</param>
    /// <returns>A task completed once the database is held exclusively and ready for operations.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    /// <exception cref="InvalidOperationException">The database is missing, uninitialized, belongs to another instance, has an unsupported schema, or is held by another process.</exception>
    /// <remarks>Calling this is optional: the first operation initializes the store. Calling it during trusted host startup surfaces a misconfigured database at boot. Repeating it after success is a no-op.</remarks>
    public ValueTask InitializeAsync(CancellationToken cancellationToken = default) => _backend.InitializeAsync(null, cancellationToken);

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

    /// <summary>Closes the connection and releases the exclusive database lock so another store may open the file.</summary>
    /// <remarks>Disposal is idempotent. Acknowledged operations were already committed and are unaffected.</remarks>
    public void Dispose() => _backend.Dispose();
}
