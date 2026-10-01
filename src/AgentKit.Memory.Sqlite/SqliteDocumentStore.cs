// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Sqlite;

using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Stores versioned documents, chunk sets, and active-version pointers in one host-local SQLite database.</summary>
/// <remarks>
/// <para>
/// Each document aggregate is one row holding its entry document, including every version, chunk set, and the active-version
/// pointer. Every mutation runs in an immediate transaction that reads stored state through the shared planner and replaces
/// that one row, so a publication and its pointer switch are a single atomic commit: a reader sees the prior complete version
/// or the new complete version, never a mix, and a failed write leaves the prior version active. Tombstones persist through
/// reopening.
/// </para>
/// <para>
/// Every operation consumes a single-use grant that binds that exact operation before the transaction opens. SQLite provides
/// durable local storage only: it implies no distributed indexing, remote replication, or atomic transaction with artifact or
/// session storage. The instance is thread-safe.
/// </para>
/// </remarks>
public sealed class SqliteDocumentStore: IDocumentStore
{
    private const string _adapter = "sqlite";

    private static readonly JsonSerializerOptions _json = JsonStoreSerialization.CreateCanonicalOptions();

    private readonly SqliteMemoryDatabase _database;
    private readonly MemoryStoreEnforcement _enforcement;
    private readonly TimeProvider _time;
    private readonly ILogger<SqliteDocumentStore> _logger;

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
    public SqliteDocumentStore(
        DocumentStoreKey key,
        SqliteMemoryTarget target,
        SqliteMemorySettings settings,
        ISecurityGrantStore grants,
        IIdentifierGenerator<SecurityEnforcementIntentId> intentIds,
        TimeProvider time,
        ILogger<SqliteDocumentStore>? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(grants);
        ArgumentNullException.ThrowIfNull(intentIds);
        ArgumentNullException.ThrowIfNull(time);
        Descriptor = new DocumentStoreDescriptor(key, "agentkit.documents.sqlite", new ComponentId("agentkit.documents.sqlite"), isDurable: true);
        _database = new SqliteMemoryDatabase(target, settings, MemoryStoreFamily.Document);
        _enforcement = new MemoryStoreEnforcement(grants, intentIds, Descriptor.SecurityAudience);
        _time = time;
        _logger = logger ?? NullLogger<SqliteDocumentStore>.Instance;
    }

    /// <inheritdoc/>
    public DocumentStoreDescriptor Descriptor { get; }

    /// <summary>Creates or validates the schema and binds the database to its expected identity.</summary>
    /// <param name="cancellationToken">Cancels before initialization completes.</param>
    /// <returns>A task completed once the database is ready for operations.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    /// <exception cref="InvalidOperationException">The database is missing, uninitialized, belongs to another instance or store family, or has an unsupported schema.</exception>
    /// <remarks>Calling this is optional: the first operation initializes the store. Repeating it after success is a no-op.</remarks>
    public ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        _database.EnsureInitialized(cancellationToken);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<DocumentWriteResult> WriteAsync(DocumentWriteRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Observe(MemoryStoreOperationKind.Write, request.Grant, async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var denial = await _enforcement.ConsumeAsync(
                request.Grant, SecurityOperationKind.StateMutation, SecurityEffect.CreateOrReplace,
                [DocumentSecurityBinding.Resource(request.Record.Id)],
                DocumentSecurityBinding.WriteFingerprint(request.Record, request.Chunks, request.Activate, request.IdempotencyKey, request.At), cancellationToken).ConfigureAwait(false);
            return denial is not null ? DocumentWriteResult.Rejected(denial) : InTransaction("write", (connection, transaction, lookup) =>
            {
                var plan = DocumentPlanner.PlanWrite(lookup, request);
                Persist(connection, transaction, plan.Upsert);
                return plan.Result;
            }, cancellationToken);
        }, static result => result.Failure);
    }

    /// <inheritdoc/>
    public ValueTask<DocumentActivateResult> ActivateAsync(DocumentActivateRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Observe(MemoryStoreOperationKind.Activate, request.Grant, async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var denial = await _enforcement.ConsumeAsync(
                request.Grant, SecurityOperationKind.StateMutation, SecurityEffect.Mutate,
                [DocumentSecurityBinding.Resource(request.Id)],
                DocumentSecurityBinding.ActivateFingerprint(request.Id, request.Version, request.ExpectedActiveVersion, request.IdempotencyKey, request.At), cancellationToken).ConfigureAwait(false);
            return denial is not null ? DocumentActivateResult.Rejected(denial) : InTransaction("activate", (connection, transaction, lookup) =>
            {
                var plan = DocumentPlanner.PlanActivate(lookup, request);
                Persist(connection, transaction, plan.Upsert);
                return plan.Result;
            }, cancellationToken);
        }, static result => result.Failure);
    }

    /// <inheritdoc/>
    public ValueTask<DocumentReadResult> ReadAsync(DocumentReadRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Observe(MemoryStoreOperationKind.Read, request.Grant, async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var denial = await _enforcement.ConsumeAsync(
                request.Grant, SecurityOperationKind.StateRead, SecurityEffect.Observe,
                [DocumentSecurityBinding.Resource(request.Id)],
                DocumentSecurityBinding.ReadFingerprint(request.Id, request.Version, request.IncludeChunks), cancellationToken).ConfigureAwait(false);
            return denial is not null ? DocumentReadResult.Rejected(denial) : InTransaction("read", (_, _, lookup) => DocumentPlanner.Read(lookup, request, Descriptor.Name), cancellationToken);
        }, static result => result.Failure);
    }

    /// <inheritdoc/>
    public ValueTask<DocumentDeleteResult> DeleteAsync(DocumentDeleteRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Observe(MemoryStoreOperationKind.Delete, request.Grant, async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var denial = await _enforcement.ConsumeAsync(
                request.Grant, SecurityOperationKind.StateMutation, SecurityEffect.Delete,
                [DocumentSecurityBinding.Resource(request.Id)],
                DocumentSecurityBinding.DeleteFingerprint(request.Id, request.Mode, request.IdempotencyKey, request.At), cancellationToken).ConfigureAwait(false);
            return denial is not null ? DocumentDeleteResult.Rejected(denial) : InTransaction("delete", (connection, transaction, lookup) =>
            {
                var plan = DocumentPlanner.PlanDelete(lookup, request, Descriptor.Name);
                Persist(connection, transaction, plan.Upsert);
                return plan.Result;
            }, cancellationToken);
        }, static result => result.Failure);
    }

    private TResult InTransaction<TResult>(
        string operation,
        Func<SqliteConnection, SqliteTransaction, SqliteDocumentLookup, TResult> work,
        CancellationToken cancellationToken)
    {
        _database.EnsureInitialized(cancellationToken);
        using var connection = _database.Open();
        using var transaction = connection.BeginTransaction(deferred: false);
        try
        {
            var result = work(connection, transaction, new SqliteDocumentLookup(connection, transaction, _json));
            cancellationToken.ThrowIfCancellationRequested();
            transaction.Commit();
            return result;
        }
        catch (SqliteException exception)
        {
            MemoryStoreObservation.Safe(() => SqliteMemoryLog.CommitFailed(_logger, "document", operation, exception.SqliteErrorCode));
            throw;
        }
    }

    private void Persist(SqliteConnection connection, SqliteTransaction transaction, DocumentEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        var document = System.Text.Encoding.UTF8.GetString(
            JsonStoreSerialization.Encode(DocumentEntryDocument.FromDomain(entry), _json, _database.Settings.MaximumRecordBytes));
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO documents(tenant, document_id, sequence, generation, document) VALUES ($tenant, $id, $sequence, $generation, $document) "
            + "ON CONFLICT(tenant, document_id) DO UPDATE SET generation = excluded.generation, document = excluded.document";
        _ = command.Parameters.AddWithValue("$tenant", entry.Tenant.Value);
        _ = command.Parameters.AddWithValue("$id", SqliteMemoryDatabase.Encode(entry.Id.Value));
        _ = command.Parameters.AddWithValue("$sequence", entry.Sequence);
        _ = command.Parameters.AddWithValue("$generation", (object?) entry.Deletion?.Generation ?? DBNull.Value);
        _ = command.Parameters.AddWithValue("$document", document);
        _ = command.ExecuteNonQuery();
    }

    private ValueTask<TResult> Observe<TResult>(
        MemoryStoreOperationKind kind,
        SecurityGrant grant,
        Func<ValueTask<TResult>> operation,
        Func<TResult, MemoryStoreFailure?> failureOf) =>
        MemoryStoreObservation.ObserveAsync(_logger, _time, _adapter, MemoryStoreFamily.Document, kind, grant.Identity.TenantId, operation, failureOf);
}
