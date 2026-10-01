// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Sqlite;

using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Searches vectors of exactly one space with a verified brute-force scan over one host-local SQLite database.</summary>
/// <remarks>
/// <para>
/// SQLite here stores vector bytes and a receipt window; it does not advertise vector search by virtue of storing them.
/// Search is an exact scan that scores every visible stored vector under the space's metric, verified by the shared vector
/// conformance suite, so <see cref="ApproximateSearch"/> is always <see langword="false"/>. No SQLite vector extension is used
/// and none is implied.
/// </para>
/// <para>
/// Every mutation runs in an immediate transaction that writes the batch, its receipt, and the new watermark together. Every
/// request names its complete vector space, and a mismatch is refused before any grant is consumed or row is read. The instance
/// is thread-safe.
/// </para>
/// </remarks>
public sealed class SqliteVectorIndex: IVectorIndex
{
    private const string _adapter = "sqlite";

    private static readonly JsonSerializerOptions _json = JsonStoreSerialization.CreateCanonicalOptions();

    private readonly SqliteMemoryDatabase _database;
    private readonly MemoryStoreEnforcement _enforcement;
    private readonly TimeProvider _time;
    private readonly ILogger<SqliteVectorIndex> _logger;

    /// <summary>Initializes an index bound to one host-authorized database without opening it.</summary>
    /// <param name="space">The single vector space the index holds.</param>
    /// <param name="target">The non-null exact database and bootstrap effects.</param>
    /// <param name="settings">The non-null immutable lock and size bounds.</param>
    /// <param name="grants">The authoritative grant store that validates and consumes each exact grant.</param>
    /// <param name="intentIds">The allocator of fresh enforcement-intent identities.</param>
    /// <param name="time">The clock used only for observational duration.</param>
    /// <param name="logger">The optional content-free logger.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    public SqliteVectorIndex(
        VectorSpaceDescriptor space,
        SqliteMemoryTarget target,
        SqliteMemorySettings settings,
        ISecurityGrantStore grants,
        IIdentifierGenerator<SecurityEnforcementIntentId> intentIds,
        TimeProvider time,
        ILogger<SqliteVectorIndex>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(space);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(grants);
        ArgumentNullException.ThrowIfNull(intentIds);
        ArgumentNullException.ThrowIfNull(time);
        VectorSpace = space;
        SecurityAudience = new ComponentId("agentkit.vectors.sqlite");
        _database = new SqliteMemoryDatabase(target, settings, MemoryStoreFamily.Vector);
        _enforcement = new MemoryStoreEnforcement(grants, intentIds, SecurityAudience);
        _time = time;
        _logger = logger ?? NullLogger<SqliteVectorIndex>.Instance;
    }

    /// <inheritdoc/>
    public VectorSpaceDescriptor VectorSpace { get; }

    /// <inheritdoc/>
    public ComponentId SecurityAudience { get; }

    /// <inheritdoc/>
    public bool IsDurable => true;

    /// <inheritdoc/>
    public bool ApproximateSearch => false;

    /// <summary>Creates or validates the schema and binds the database to its expected identity.</summary>
    /// <param name="cancellationToken">Cancels before initialization completes.</param>
    /// <returns>A task completed once the database is ready for operations.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    /// <exception cref="InvalidOperationException">The database is missing, uninitialized, belongs to another instance or store family, or has an unsupported schema.</exception>
    /// <remarks>Calling this is optional: the first operation initializes the index. Repeating it after success is a no-op.</remarks>
    public ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        _database.EnsureInitialized(cancellationToken);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<VectorUpsertResult> UpsertAsync(VectorUpsertRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Observe(MemoryStoreOperationKind.Upsert, request.Grant, async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (VectorPlanner.CheckSpace(VectorSpace, request.Space) is { } mismatch)
            {
                return VectorUpsertResult.Rejected(mismatch);
            }

            var denial = await _enforcement.ConsumeAsync(
                request.Grant, SecurityOperationKind.StateMutation, SecurityEffect.CreateOrReplace,
                [VectorSecurityBinding.Resource(VectorSpace.IndexKey)],
                VectorSecurityBinding.UpsertFingerprint(request.Space, request.Records, request.IdempotencyKey), cancellationToken).ConfigureAwait(false);
            return denial is not null ? VectorUpsertResult.Rejected(denial) : InTransaction("upsert", (connection, transaction, lookup) =>
            {
                var plan = VectorPlanner.PlanUpsert(lookup, request);
                Persist(connection, transaction, plan.Upserts, plan.Deletes, plan.Receipt, plan.Watermark);
                return plan.Result;
            }, cancellationToken);
        }, static result => result.Failure);
    }

    /// <inheritdoc/>
    public ValueTask<VectorSearchResult> SearchAsync(VectorSearchRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Observe(MemoryStoreOperationKind.Search, request.Grant, async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (VectorPlanner.CheckSpace(VectorSpace, request.Space) is { } mismatch)
            {
                return VectorSearchResult.Rejected(mismatch);
            }

            var denial = await _enforcement.ConsumeAsync(
                request.Grant, SecurityOperationKind.StateRead, SecurityEffect.Observe,
                [VectorSecurityBinding.Resource(VectorSpace.IndexKey)],
                VectorSecurityBinding.SearchFingerprint(request.Space, request.Query, request.TopK, request.Documents), cancellationToken).ConfigureAwait(false);
            return denial is not null ? VectorSearchResult.Rejected(denial) : InTransaction("search", (_, _, lookup) => VectorPlanner.Search(lookup, VectorSpace, request), cancellationToken);
        }, static result => result.Failure);
    }

    /// <inheritdoc/>
    public ValueTask<VectorDeleteResult> DeleteAsync(VectorDeleteRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Observe(MemoryStoreOperationKind.Delete, request.Grant, async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (VectorPlanner.CheckSpace(VectorSpace, request.Space) is { } mismatch)
            {
                return VectorDeleteResult.Rejected(mismatch);
            }

            var denial = await _enforcement.ConsumeAsync(
                request.Grant, SecurityOperationKind.StateMutation, SecurityEffect.Delete,
                [VectorSecurityBinding.Resource(VectorSpace.IndexKey)],
                VectorSecurityBinding.DeleteFingerprint(request.Space, request.ChunkIds, request.IdempotencyKey), cancellationToken).ConfigureAwait(false);
            return denial is not null ? VectorDeleteResult.Rejected(denial) : InTransaction("delete", (connection, transaction, lookup) =>
            {
                var plan = VectorPlanner.PlanDelete(lookup, request);
                Persist(connection, transaction, plan.Upserts, plan.Deletes, plan.Receipt, plan.Watermark);
                return plan.Result;
            }, cancellationToken);
        }, static result => result.Failure);
    }

    private TResult InTransaction<TResult>(
        string operation,
        Func<SqliteConnection, SqliteTransaction, SqliteVectorLookup, TResult> work,
        CancellationToken cancellationToken)
    {
        _database.EnsureInitialized(cancellationToken);
        using var connection = _database.Open();
        using var transaction = connection.BeginTransaction(deferred: false);
        try
        {
            var result = work(connection, transaction, new SqliteVectorLookup(connection, transaction, _json));
            cancellationToken.ThrowIfCancellationRequested();
            transaction.Commit();
            return result;
        }
        catch (SqliteException exception)
        {
            MemoryStoreObservation.Safe(() => SqliteMemoryLog.CommitFailed(_logger, "vector", operation, exception.SqliteErrorCode));
            throw;
        }
    }

    private void Persist(
        SqliteConnection connection,
        SqliteTransaction transaction,
        ImmutableArray<VectorEntry> upserts,
        ImmutableArray<ChunkId> deletes,
        VectorReceipt? receipt,
        long watermark)
    {
        if (receipt is null)
        {
            return;
        }

        foreach (var entry in upserts)
        {
            var document = System.Text.Encoding.UTF8.GetString(
                JsonStoreSerialization.Encode(VectorRecordDocument.FromDomain(entry.Record), _json, _database.Settings.MaximumRecordBytes));
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "INSERT INTO vectors(tenant, chunk_id, agent_id, document) VALUES ($tenant, $chunk, $agent, $document) "
                + "ON CONFLICT(tenant, chunk_id) DO UPDATE SET agent_id = excluded.agent_id, document = excluded.document";
            _ = command.Parameters.AddWithValue("$tenant", entry.Tenant.Value);
            _ = command.Parameters.AddWithValue("$chunk", SqliteMemoryDatabase.Encode(entry.Record.ChunkId.Value));
            _ = command.Parameters.AddWithValue("$agent", SqliteMemoryDatabase.Encode(entry.Record.AgentId.Value));
            _ = command.Parameters.AddWithValue("$document", document);
            _ = command.ExecuteNonQuery();
        }

        foreach (var chunk in deletes)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "DELETE FROM vectors WHERE tenant = $tenant AND chunk_id = $chunk";
            _ = command.Parameters.AddWithValue("$tenant", receipt.Tenant.Value);
            _ = command.Parameters.AddWithValue("$chunk", SqliteMemoryDatabase.Encode(chunk.Value));
            _ = command.ExecuteNonQuery();
        }

        using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO vector_receipts(tenant, receipt_key, document) VALUES ($tenant, $key, $document)";
            _ = insert.Parameters.AddWithValue("$tenant", receipt.Tenant.Value);
            _ = insert.Parameters.AddWithValue("$key", receipt.Key);
            _ = insert.Parameters.AddWithValue("$document", System.Text.Encoding.UTF8.GetString(
                JsonStoreSerialization.Encode(VectorReceiptDocument.FromDomain(receipt), _json, _database.Settings.MaximumRecordBytes)));
            _ = insert.ExecuteNonQuery();
        }

        using (var prune = connection.CreateCommand())
        {
            prune.Transaction = transaction;
            prune.CommandText = "DELETE FROM vector_receipts WHERE position <= (SELECT MAX(position) FROM vector_receipts) - $window";
            _ = prune.Parameters.AddWithValue("$window", VectorStoreState.ReceiptWindow);
            _ = prune.ExecuteNonQuery();
        }

        using var advance = connection.CreateCommand();
        advance.Transaction = transaction;
        advance.CommandText = "UPDATE vector_metadata SET watermark = MAX(watermark, $watermark)";
        _ = advance.Parameters.AddWithValue("$watermark", watermark);
        _ = advance.ExecuteNonQuery();
    }

    private ValueTask<TResult> Observe<TResult>(
        MemoryStoreOperationKind kind,
        SecurityGrant grant,
        Func<ValueTask<TResult>> operation,
        Func<TResult, MemoryStoreFailure?> failureOf) =>
        MemoryStoreObservation.ObserveAsync(_logger, _time, _adapter, MemoryStoreFamily.Vector, kind, grant.Identity.TenantId, operation, failureOf);
}
