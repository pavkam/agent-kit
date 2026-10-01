// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Sqlite;

using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Stores durable memory records in one host-local SQLite database.</summary>
/// <remarks>
/// <para>
/// Each memory aggregate is one row holding its entry document, with indexed columns for tenant, agent, creation sequence,
/// and deletion generation. Every mutation runs in an immediate transaction that reads stored state through the shared
/// planner and writes the result, so the version check, idempotent replay, sequence allocation, and deletion-generation
/// assignment are atomic even across processes sharing the file. An acknowledged write is committed and survives process loss,
/// and a tombstone persists through reopening so a restored database cannot resurrect a deleted record.
/// </para>
/// <para>
/// Every operation consumes a single-use grant that binds that exact operation before the transaction opens. SQLite provides
/// durable local storage only: it implies no distributed lease, fencing, or cross-store atomicity. The instance is thread-safe.
/// </para>
/// </remarks>
public sealed class SqliteMemoryStore: IMemoryStore
{
    private const string _adapter = "sqlite";

    private static readonly JsonSerializerOptions _json = JsonStoreSerialization.CreateCanonicalOptions();

    private readonly SqliteMemoryDatabase _database;
    private readonly MemoryStoreEnforcement _enforcement;
    private readonly TimeProvider _time;
    private readonly ILogger<SqliteMemoryStore> _logger;

    /// <summary>Initializes a store bound to one host-authorized database without opening it.</summary>
    /// <param name="key">The key the store is registered and selected under.</param>
    /// <param name="target">The non-null exact database and bootstrap effects.</param>
    /// <param name="settings">The non-null immutable lock and size bounds.</param>
    /// <param name="grants">The authoritative grant store that validates and consumes each exact grant.</param>
    /// <param name="intentIds">The allocator of fresh enforcement-intent identities.</param>
    /// <param name="time">The clock used only for observational duration.</param>
    /// <param name="logger">The optional content-free logger.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> is default or blank.</exception>
    public SqliteMemoryStore(
        MemoryStoreKey key,
        SqliteMemoryTarget target,
        SqliteMemorySettings settings,
        ISecurityGrantStore grants,
        IIdentifierGenerator<SecurityEnforcementIntentId> intentIds,
        TimeProvider time,
        ILogger<SqliteMemoryStore>? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(grants);
        ArgumentNullException.ThrowIfNull(intentIds);
        ArgumentNullException.ThrowIfNull(time);
        Descriptor = new MemoryStoreDescriptor(key, "agentkit.memory.sqlite", new ComponentId("agentkit.memory.sqlite"), isDurable: true);
        _database = new SqliteMemoryDatabase(target, settings, MemoryStoreFamily.Memory);
        _enforcement = new MemoryStoreEnforcement(grants, intentIds, Descriptor.SecurityAudience);
        _time = time;
        _logger = logger ?? NullLogger<SqliteMemoryStore>.Instance;
    }

    /// <inheritdoc/>
    public MemoryStoreDescriptor Descriptor { get; }

    /// <summary>Creates or validates the schema and binds the database to its expected identity.</summary>
    /// <param name="cancellationToken">Cancels before initialization completes.</param>
    /// <returns>A task completed once the database is ready for operations.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    /// <exception cref="InvalidOperationException">The database is missing, uninitialized, belongs to another instance or store family, or has an unsupported schema.</exception>
    /// <remarks>Calling this is optional: the first operation initializes the store. Calling it during trusted host startup surfaces a misconfigured database at boot. Repeating it after success is a no-op.</remarks>
    public ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        _database.EnsureInitialized(cancellationToken);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<MemoryWriteResult> WriteAsync(MemoryWriteRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Observe(MemoryStoreOperationKind.Write, request.Grant, async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var denial = await _enforcement.ConsumeAsync(
                request.Grant, SecurityOperationKind.StateMutation, SecurityEffect.Create,
                [MemorySecurityBinding.Resource(request.Record.Id)], MemorySecurityBinding.WriteFingerprint(request.Record, request.IdempotencyKey), cancellationToken).ConfigureAwait(false);
            return denial is not null ? MemoryWriteResult.Rejected(denial) : InTransaction("write", (connection, transaction, lookup) =>
            {
                var plan = MemoryPlanner.PlanWrite(lookup, request);
                Persist(connection, transaction, plan.Upserts);
                return plan.Result;
            }, cancellationToken);
        }, static result => result.Failure);
    }

    /// <inheritdoc/>
    public ValueTask<MemoryReadResult> ReadAsync(MemoryReadRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Observe(MemoryStoreOperationKind.Read, request.Grant, async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var denial = await _enforcement.ConsumeAsync(
                request.Grant, SecurityOperationKind.StateRead, SecurityEffect.Observe,
                [MemorySecurityBinding.Resource(request.Id)], MemorySecurityBinding.ReadFingerprint(request.Id), cancellationToken).ConfigureAwait(false);
            return denial is not null ? MemoryReadResult.Rejected(denial) : InTransaction("read", (_, _, lookup) => MemoryPlanner.Read(lookup, request), cancellationToken);
        }, static result => result.Failure);
    }

    /// <inheritdoc/>
    public ValueTask<MemoryListResult> ListAsync(MemoryListRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Observe(MemoryStoreOperationKind.List, request.Grant, async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var denial = await _enforcement.ConsumeAsync(
                request.Grant, SecurityOperationKind.StateRead, SecurityEffect.Observe,
                [MemorySecurityBinding.CollectionResource(request.Grant.Scope.AgentId)],
                MemorySecurityBinding.ListFingerprint(request.Namespace, request.States, request.Terms, request.AfterSequence, request.Limit), cancellationToken).ConfigureAwait(false);
            return denial is not null ? MemoryListResult.Rejected(denial) : InTransaction("list", (_, _, lookup) => MemoryPlanner.List(lookup, request), cancellationToken);
        }, static result => result.Failure);
    }

    /// <inheritdoc/>
    public ValueTask<MemoryTransitionResult> TransitionAsync(MemoryTransitionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Observe(MemoryStoreOperationKind.Transition, request.Grant, async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var denial = await _enforcement.ConsumeAsync(
                request.Grant, SecurityOperationKind.StateMutation, SecurityEffect.Mutate,
                [MemorySecurityBinding.Resource(request.Id)],
                MemorySecurityBinding.TransitionFingerprint(request.Id, request.To, request.ExpectedVersion, request.Replacement, request.IdempotencyKey, request.At), cancellationToken).ConfigureAwait(false);
            return denial is not null ? MemoryTransitionResult.Rejected(denial) : InTransaction("transition", (connection, transaction, lookup) =>
            {
                var plan = MemoryPlanner.PlanTransition(lookup, request);
                Persist(connection, transaction, plan.Upserts);
                return plan.Result;
            }, cancellationToken);
        }, static result => result.Failure);
    }

    /// <inheritdoc/>
    public ValueTask<MemoryDeleteResult> DeleteAsync(MemoryDeleteRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Observe(MemoryStoreOperationKind.Delete, request.Grant, async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var denial = await _enforcement.ConsumeAsync(
                request.Grant, SecurityOperationKind.StateMutation, SecurityEffect.Delete,
                [MemorySecurityBinding.Resource(request.Id)],
                MemorySecurityBinding.DeleteFingerprint(request.Id, request.ExpectedVersion, request.Mode, request.IdempotencyKey, request.At), cancellationToken).ConfigureAwait(false);
            return denial is not null ? MemoryDeleteResult.Rejected(denial) : InTransaction("delete", (connection, transaction, lookup) =>
            {
                var plan = MemoryPlanner.PlanDelete(lookup, request, Descriptor.Name);
                Persist(connection, transaction, plan.Upserts);
                return plan.Result;
            }, cancellationToken);
        }, static result => result.Failure);
    }

    private TResult InTransaction<TResult>(
        string operation,
        Func<SqliteConnection, SqliteTransaction, SqliteMemoryLookup, TResult> work,
        CancellationToken cancellationToken)
    {
        _database.EnsureInitialized(cancellationToken);
        using var connection = _database.Open();
        using var transaction = connection.BeginTransaction(deferred: false);
        try
        {
            var result = work(connection, transaction, new SqliteMemoryLookup(connection, transaction, _json));
            cancellationToken.ThrowIfCancellationRequested();
            transaction.Commit();
            return result;
        }
        catch (SqliteException exception)
        {
            MemoryStoreObservation.Safe(() => SqliteMemoryLog.CommitFailed(_logger, "memory", operation, exception.SqliteErrorCode));
            throw;
        }
    }

    private void Persist(SqliteConnection connection, SqliteTransaction transaction, ImmutableArray<MemoryEntry> upserts)
    {
        foreach (var entry in upserts)
        {
            var document = System.Text.Encoding.UTF8.GetString(
                JsonStoreSerialization.Encode(MemoryEntryDocument.FromDomain(entry), _json, _database.Settings.MaximumRecordBytes));
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "INSERT INTO memories(tenant, memory_id, agent_id, sequence, generation, document) VALUES ($tenant, $id, $agent, $sequence, $generation, $document) "
                + "ON CONFLICT(tenant, memory_id) DO UPDATE SET generation = excluded.generation, document = excluded.document";
            _ = command.Parameters.AddWithValue("$tenant", entry.Tenant.Value);
            _ = command.Parameters.AddWithValue("$id", SqliteMemoryDatabase.Encode(entry.Current.Id.Value));
            _ = command.Parameters.AddWithValue("$agent", SqliteMemoryDatabase.Encode(entry.Current.AgentId.Value));
            _ = command.Parameters.AddWithValue("$sequence", entry.Sequence);
            _ = command.Parameters.AddWithValue("$generation", (object?) entry.Deletion?.Generation ?? DBNull.Value);
            _ = command.Parameters.AddWithValue("$document", document);
            _ = command.ExecuteNonQuery();
            using var creation = connection.CreateCommand();
            creation.Transaction = transaction;
            creation.CommandText = "INSERT OR IGNORE INTO memory_creations(tenant, creation_key, memory_id) VALUES ($tenant, $key, $id)";
            _ = creation.Parameters.AddWithValue("$tenant", entry.Tenant.Value);
            _ = creation.Parameters.AddWithValue("$key", entry.CreateKey);
            _ = creation.Parameters.AddWithValue("$id", SqliteMemoryDatabase.Encode(entry.Current.Id.Value));
            _ = creation.ExecuteNonQuery();
        }
    }

    private ValueTask<TResult> Observe<TResult>(
        MemoryStoreOperationKind kind,
        SecurityGrant grant,
        Func<ValueTask<TResult>> operation,
        Func<TResult, MemoryStoreFailure?> failureOf) =>
        MemoryStoreObservation.ObserveAsync(_logger, _time, _adapter, MemoryStoreFamily.Memory, kind, grant.Identity.TenantId, operation, failureOf);
}
