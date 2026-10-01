// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Sqlite;

/// <summary>Holds caller-owned reference-commit intents durably in one host-local, exclusively held SQLite database.</summary>
/// <remarks>
/// <para>
/// Every recorded intent and transition is committed before it is acknowledged and survives close and reopen, so a crash between
/// artifact finalization and the caller's reference commit leaves an explicit pending intent for reconciliation. A late reference
/// commit and a fence race through one conditional transition under a lock, so exactly one wins; every operation is tenant-qualified,
/// so another tenant's intent is indistinguishable from an absent one. Storage failures propagate as exceptions and leave the previous
/// state intact.
/// </para>
/// <para>
/// The database must not be shared with an artifact store or another intent store: it carries its own schema and instance identity and
/// is held exclusively while the store is open. The adapter advertises durable local state only, with no distributed fencing and no
/// atomic transaction with the caller's session, tool, or memory records. The instance is thread-safe and owns its connection;
/// dispose it to release the file.
/// </para>
/// </remarks>
public sealed class SqliteArtifactReferenceCommitIntentStore: IArtifactReferenceCommitIntentStore, IDisposable
{
    private readonly SqliteArtifactReferenceCommitIntentBackend _backend;

    /// <summary>Initializes a store bound to one host-authorized database without opening it.</summary>
    /// <param name="target">The non-null exact database and bootstrap effects.</param>
    /// <param name="settings">The non-null immutable lock and size bounds.</param>
    /// <param name="time">The clock used only for observational duration.</param>
    /// <param name="logger">The optional content-free logger.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    public SqliteArtifactReferenceCommitIntentStore(
        SqliteArtifactTarget target,
        SqliteArtifactSettings settings,
        TimeProvider time,
        ILogger<SqliteArtifactReferenceCommitIntentStore>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(time);
        _backend = new SqliteArtifactReferenceCommitIntentBackend(
            new SqliteArtifactDatabase(target, settings, SqliteArtifactSchema.Intents), time, logger);
    }

    /// <summary>Creates or validates the schema, binds the database to its expected identity, and replays every persisted intent.</summary>
    /// <param name="cancellationToken">Cancels before initialization completes.</param>
    /// <returns>A task completed once the database is held exclusively and ready for operations.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    /// <exception cref="InvalidOperationException">The database is missing, uninitialized, belongs to another instance or store kind, has an unsupported schema, or is held by another process.</exception>
    /// <remarks>Calling this is optional: the first operation initializes the store. Repeating it after success is a no-op.</remarks>
    public ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        _backend.Initialize(cancellationToken);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<ArtifactReferenceCommitIntentResult> RecordAsync(ArtifactReferenceCommitIntent intent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(intent);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(_backend.Record(intent, cancellationToken));
    }

    /// <inheritdoc/>
    public ValueTask<ArtifactReferenceCommitIntentResult> GetAsync(TenantId tenantId, ArtifactPreparationId preparationId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId.Value, nameof(tenantId));
        ArgumentOutOfRangeException.ThrowIfEqual(preparationId, default);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(_backend.Get(tenantId, preparationId, cancellationToken));
    }

    /// <inheritdoc/>
    public ValueTask<ArtifactReferenceCommitIntentResult> TransitionAsync(
        TenantId tenantId,
        ArtifactPreparationId preparationId,
        ArtifactReferenceCommitState expected,
        ArtifactReferenceCommitState nextState,
        DateTimeOffset at,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId.Value, nameof(tenantId));
        ArgumentOutOfRangeException.ThrowIfEqual(preparationId, default);
        ArgumentOutOfRangeException.ThrowIfUndefined(expected);
        ArgumentOutOfRangeException.ThrowIfUndefined(nextState);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(_backend.Transition(tenantId, preparationId, expected, nextState, at, cancellationToken));
    }

    /// <inheritdoc/>
    public ValueTask<ImmutableArray<ArtifactReferenceCommitIntent>> ListPendingAsync(
        TenantId tenantId,
        DateTimeOffset recordedBefore,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId.Value, nameof(tenantId));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(_backend.ListPending(tenantId, recordedBefore, limit, cancellationToken));
    }

    /// <summary>Closes the connection and releases the exclusive database lock so another store may open the file.</summary>
    /// <remarks>Disposal is idempotent. Acknowledged operations were already committed and are unaffected.</remarks>
    public void Dispose() => _backend.Dispose();
}
