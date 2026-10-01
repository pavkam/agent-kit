// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Sqlite;

using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Records durable operation evidence in one fixed local SQLite database.</summary>
/// <remarks>
/// <para>
/// Every write reads the operation's current row, evaluates the shared lifecycle and fencing rules, and replaces the row
/// inside one immediate SQLite transaction, so a concurrent writer either sees the complete previous state or the
/// complete new one. Fencing is enforced against the persisted last-writer generation rather than an in-process
/// counter, which is what lets a second process take over safely.
/// </para>
/// <para>
/// Journal access is protected. Each write consumes its single-use grant and completes required audit before the
/// transaction opens, and an authorized evidence read is unfenced so a recovering worker can learn what happened before
/// it seeks ownership. The journal fails closed: an unavailable grant store or audit dispatcher denies the operation
/// instead of writing unaudited state.
/// </para>
/// <para>
/// This adapter is durable across process restarts on one host. SQLite coordinates writers on one machine; it provides
/// no distributed lease, no cross-host fencing, and no multi-store atomicity, so a composition that spans hosts needs a
/// distributed backend rather than a shared file.
/// </para>
/// </remarks>
public sealed class SqliteDurableOperationJournal: IDurableOperationJournal
{
    private readonly SqliteDurableDatabase _database;
    private readonly DurableJournalEnforcement _enforcement;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SqliteDurableOperationJournal> _logger;

    /// <summary>Initializes a journal bound to one selected key, one host-authorized database, and its required security dependencies.</summary>
    /// <param name="key">The exact registration key every authorized request must target.</param>
    /// <param name="database">The non-null shared database boundary this journal and its lease manager commit against.</param>
    /// <param name="auditRecordIds">The non-null generator for each required audit record's stable identity.</param>
    /// <param name="auditDispatcher">The non-null required audit dispatcher that must accept every consumed access intent.</param>
    /// <param name="grants">The non-null authoritative store that validates and consumes each exact single-use journal grant.</param>
    /// <param name="timeProvider">The non-null injected clock used for commit timestamps and elapsed measurement.</param>
    /// <param name="logger">The optional content-free structured logger.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> carries no key text.</exception>
    /// <remarks>The security dependencies are required at construction rather than per call, so a composition cannot produce a journal that silently skips grant consumption or audit.</remarks>
    public SqliteDurableOperationJournal(
        DurableJournalKey key,
        SqliteDurableDatabase database,
        IIdentifierGenerator<SecurityAuditRecordId> auditRecordIds,
        ISecurityAuditDispatcher auditDispatcher,
        ISecurityGrantStore grants,
        TimeProvider timeProvider,
        ILogger<SqliteDurableOperationJournal>? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(timeProvider);
        Key = key;
        _database = database;
        _enforcement = new DurableJournalEnforcement(
            key, SecurityAudience, auditRecordIds, auditDispatcher, grants, timeProvider);
        _timeProvider = timeProvider;
        _logger = logger ?? NullLogger<SqliteDurableOperationJournal>.Instance;
    }

    /// <summary>Gets the registration key this journal instance answers for.</summary>
    /// <value>The nonblank key an <see cref="AuthorizedDurableRequest{TRequest}"/> must name to reach this journal.</value>
    public DurableJournalKey Key { get; }

    /// <inheritdoc/>
    /// <value>The stable component identity of this adapter. A grant minted for another audience is refused.</value>
    public ComponentId SecurityAudience { get; } = new("agentkit.durability.sqlite");

    /// <summary>Creates or validates the exact version-one schema and persistent store identity under bootstrap policy.</summary>
    /// <param name="cancellationToken">Cancels before the schema transaction commits or before a permitted journal-mode change begins.</param>
    /// <returns>A task completed after the exact target is ready for journal operations.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before the applicable bootstrap linearization point.</exception>
    /// <exception cref="InvalidOperationException">The target, schema, store identity, encoding contract, or persistence provider cannot be validated safely.</exception>
    /// <remarks>Call this once during trusted host startup. Initializing the shared database more than once, including from the lease manager, is a no-op.</remarks>
    public ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        _database.Initialize(cancellationToken);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<DurableRecordResult> RecordStartAsync(
        AuthorizedDurableRequest<DurableOperationStart> start,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(start);
        var request = start.Request;
        var binding = request.Descriptor.Binding;
        return WriteAsync(
            DurableJournalWriteOperation.RecordStart,
            start,
            binding.Address,
            binding.ExecutionContext.Authorization,
            request.FencingToken,
            DurableJournalSecurityBinding.Fingerprint(request),
            SecurityEffect.Create,
            (connection, transaction, receipt) => CommitStart(connection, transaction, request, receipt),
            cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<DurableRecordResult> RecordCheckpointAsync(
        AuthorizedDurableRequest<DurableCheckpoint> checkpoint,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        var request = checkpoint.Request;
        return WriteAsync(
            DurableJournalWriteOperation.RecordCheckpoint,
            checkpoint,
            request.Address,
            request.ExecutionContext.Authorization,
            request.FencingToken,
            DurableJournalSecurityBinding.Fingerprint(request),
            SecurityEffect.Append,
            (connection, transaction, receipt) => CommitCheckpoint(connection, transaction, request, receipt),
            cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<DurableRecordResult> RecordTerminalAsync(
        AuthorizedDurableRequest<DurableOperationResult> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        var request = result.Request;
        return WriteAsync(
            DurableJournalWriteOperation.RecordTerminal,
            result,
            request.Address,
            request.ExecutionContext.Authorization,
            request.FencingToken,
            DurableJournalSecurityBinding.Fingerprint(request),
            SecurityEffect.Append,
            (connection, transaction, receipt) => CommitTerminal(connection, transaction, request, receipt),
            cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<DurableRecordResult> RecordWaitingAsync(
        AuthorizedDurableRequest<DurableOperationWaiting> waiting,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(waiting);
        var request = waiting.Request;
        return WriteAsync(
            DurableJournalWriteOperation.RecordWaiting,
            waiting,
            request.Address,
            request.ExecutionContext.Authorization,
            request.FencingToken,
            DurableJournalSecurityBinding.Fingerprint(request),
            SecurityEffect.Mutate,
            (connection, transaction, receipt) => CommitWaiting(connection, transaction, request, receipt),
            cancellationToken);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The read is authorized and audited but unfenced: a recovering worker must be able to learn what happened before
    /// it seeks ownership, so presenting a fence it has not acquired is refused rather than required. A bare address
    /// carries no captured context, so authorization comes from the presented grant's own capture, which must describe
    /// the address exactly.
    /// </remarks>
    public async ValueTask<RecoveryEvidenceResult> LoadEvidenceAsync(
        AuthorizedDurableRequest<DurableOperationAddress> address,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(address);
        var operationAddress = address.Request;
        var started = DurableStorageDiagnostics.TryGetTimestamp(_timeProvider);
        using var activityScope = AgentKitActivityScope.Start(
            AgentKitActivityNames.DurableJournalLoadEvidence,
            ActivityKind.Internal,
            CreateTags(operationAddress));
        var activity = activityScope.Activity;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            _database.RequireInitialized("operation journal");
            var (_, denial) = await _enforcement.EnforceAsync(
                address,
                operationAddress,
                address.Grant.Authorization,
                requiredFence: null,
                DurableJournalSecurityBinding.Fingerprint(operationAddress),
                SecurityOperationKind.StateRead,
                SecurityEffect.Observe,
                cancellationToken).ConfigureAwait(false);
            if (denial is not null)
            {
                FinishEvidenceLoad(activity, DurableJournalEvidenceOutcome.Denied, started, null);
                return new RecoveryEvidenceUnavailable(denial);
            }

            cancellationToken.ThrowIfCancellationRequested();
            using var connection = _database.OpenValidated();
            using var transaction = connection.BeginTransaction(deferred: false);
            _database.Validate(connection, requireWal: true, performIntegrityCheck: false, transaction);
            cancellationToken.ThrowIfCancellationRequested();
            var existing = TryRead(connection, transaction, operationAddress);
            cancellationToken.ThrowIfCancellationRequested();
            transaction.Commit();
            if (existing is null)
            {
                FinishEvidenceLoad(activity, DurableJournalEvidenceOutcome.NotFound, started, null);
                return new RecoveryEvidenceNotFound(operationAddress);
            }

            FinishEvidenceLoad(activity, DurableJournalEvidenceOutcome.Loaded, started, null);
            return new RecoveryEvidenceLoaded(existing.ToEvidence());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            FinishEvidenceLoad(
                activity, DurableJournalEvidenceOutcome.Cancelled, started, nameof(OperationCanceledException));
            throw;
        }
    }

    private DurableRecordResult CommitStart(
        SqliteConnection connection,
        SqliteTransaction transaction,
        DurableOperationStart start,
        DurableJournalEnforcementReceipt receipt)
    {
        Debug.Assert(start is not null, "An acceptance declaration is required.");
        var address = start.Descriptor.Binding.Address;
        var existing = TryRead(connection, transaction, address);
        if (DurableJournalTransition.RejectStart(existing, start) is { } rejection)
        {
            return rejection;
        }

        // Read the clock before mutating: if GetUtcNow() throws after the write, the caller would see an exception
        // for a record the journal had already committed, bypassing the DurableRecordFailed(committed: …) channel
        // meant to report exactly that.
        var recordedAt = _timeProvider.GetUtcNow();
        Persist(connection, transaction, address, DurableJournalTransition.ApplyStart(existing, start));
        transaction.Commit();
        return new DurableRecorded(start.FencingToken, recordedAt, receipt);
    }

    private DurableRecordResult CommitCheckpoint(
        SqliteConnection connection,
        SqliteTransaction transaction,
        DurableCheckpoint checkpoint,
        DurableJournalEnforcementReceipt receipt)
    {
        Debug.Assert(checkpoint is not null, "A checkpoint is required.");
        var existing = TryRead(connection, transaction, checkpoint.Address);
        if (DurableJournalTransition.RejectCheckpoint(existing, checkpoint) is { } rejection)
        {
            return rejection;
        }

        Debug.Assert(existing is not null, "A permitted checkpoint always targets an existing projection.");

        // Read the clock before mutating; see the identical comment in CommitStart.
        var recordedAt = _timeProvider.GetUtcNow();
        DurableJournalTransition.ApplyCheckpoint(existing, checkpoint);
        Persist(connection, transaction, checkpoint.Address, existing);
        transaction.Commit();
        return new DurableRecorded(checkpoint.FencingToken, recordedAt, receipt);
    }

    private DurableRecordResult CommitTerminal(
        SqliteConnection connection,
        SqliteTransaction transaction,
        DurableOperationResult terminal,
        DurableJournalEnforcementReceipt receipt)
    {
        Debug.Assert(terminal is not null, "A terminal result is required.");
        var existing = TryRead(connection, transaction, terminal.Address);
        if (DurableJournalTransition.RejectTerminal(existing, terminal) is { } rejection)
        {
            return rejection;
        }

        Debug.Assert(existing is not null, "A permitted terminal record always targets an existing projection.");
        if (DurableJournalTransition.RepeatsTerminal(existing, terminal))
        {
            return new DurableRecorded(terminal.FencingToken, _timeProvider.GetUtcNow(), receipt);
        }

        // Read the clock before mutating; see the identical comment in CommitStart.
        var recordedAt = _timeProvider.GetUtcNow();
        DurableJournalTransition.ApplyTerminal(existing, terminal);
        Persist(connection, transaction, terminal.Address, existing);
        transaction.Commit();
        return new DurableRecorded(terminal.FencingToken, recordedAt, receipt);
    }

    private DurableRecordResult CommitWaiting(
        SqliteConnection connection,
        SqliteTransaction transaction,
        DurableOperationWaiting waiting,
        DurableJournalEnforcementReceipt receipt)
    {
        Debug.Assert(waiting is not null, "A waiting record is required.");
        var existing = TryRead(connection, transaction, waiting.Address);
        if (DurableJournalTransition.RejectWaiting(existing, waiting) is { } rejection)
        {
            return rejection;
        }

        Debug.Assert(existing is not null, "A permitted waiting record always targets an existing projection.");

        // Read the clock before mutating; see the identical comment in CommitStart.
        var recordedAt = _timeProvider.GetUtcNow();
        DurableJournalTransition.ApplyWaiting(existing, waiting);
        Persist(connection, transaction, waiting.Address, existing);
        transaction.Commit();
        return new DurableRecorded(waiting.FencingToken, recordedAt, receipt);
    }

    private DurableOperationProjection? TryRead(
        SqliteConnection connection,
        SqliteTransaction transaction,
        DurableOperationAddress address)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT projection, projection_digest FROM durable_operations
            WHERE agent_id = $agent AND session_id = $session AND run_id = $run
              AND operation_id = $operation AND turn_id = $turn;
            """;
        SqliteDurableDatabase.EncodeKey(address).Bind(command);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        var payload = SqliteDurableDatabase.ReadBoundedBlob(reader, 0, _database.Settings.MaximumRecordBytes);
        SqliteDurableDatabase.VerifyDigest(
            payload, SqliteDurableDatabase.ReadBoundedBlob(reader, 1, SHA256.HashSizeInBytes));
        return SqliteDurableCodec.DecodeProjection(payload, _database.Settings);
    }

    private void Persist(
        SqliteConnection connection,
        SqliteTransaction transaction,
        DurableOperationAddress address,
        DurableOperationProjection projection)
    {
        Debug.Assert(projection is not null, "A projection to persist is required.");
        var payload = SqliteDurableCodec.EncodeProjection(projection, _database.Settings);
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO durable_operations
                (agent_id, session_id, run_id, operation_id, turn_id, state, last_writer_token, projection, projection_digest)
            VALUES ($agent, $session, $run, $operation, $turn, $state, $token, $projection, $digest)
            ON CONFLICT(agent_id, session_id, run_id, operation_id, turn_id) DO UPDATE SET
                state = excluded.state,
                last_writer_token = excluded.last_writer_token,
                projection = excluded.projection,
                projection_digest = excluded.projection_digest;
            """;
        SqliteDurableDatabase.EncodeKey(address).Bind(command);
        _ = command.Parameters.AddWithValue("$state", (int) projection.State);
        _ = command.Parameters.AddWithValue("$token", projection.LastWriterToken.Value);
        _ = command.Parameters.AddWithValue("$projection", payload);
        _ = command.Parameters.AddWithValue("$digest", SHA256.HashData(payload));
        if (command.ExecuteNonQuery() != 1)
        {
            throw SqliteDurableDatabase.Unavailable(
                SqliteDurableDatabase.PersistenceFailed,
                "The SQLite durable journal write could not be committed atomically.");
        }
    }

    /// <summary>Authorizes, audits, and then runs one write body inside an immediate transaction with shared diagnostics.</summary>
    /// <typeparam name="TRequest">The immutable journal request shape.</typeparam>
    /// <param name="operation">The bounded write-stage dimension.</param>
    /// <param name="request">The protected request carrying the grant and enforcement intent.</param>
    /// <param name="address">The operation coordinates the write targets.</param>
    /// <param name="authorization">The capture the write must run under.</param>
    /// <param name="fencingToken">The ownership generation the write presents; it is also the required intent fence.</param>
    /// <param name="fingerprint">The canonical digest over the complete request.</param>
    /// <param name="effect">The protected effect this write performs.</param>
    /// <param name="body">The commit body executed inside the transaction after authorization succeeds; it commits on success.</param>
    /// <param name="cancellationToken">Cancels the attempt before it commits.</param>
    /// <returns>The write body's terminal result, or a non-committed failure when authorization denied it.</returns>
    /// <remarks>
    /// Authorization strictly precedes the transaction, so a denial can never leave partial state. A provider failure
    /// becomes a failure result with unknown commit status rather than an exception, because a caller that cannot tell
    /// whether the record landed must not be told that it definitely did not. Diagnostics are observational.
    /// </remarks>
    private async ValueTask<DurableRecordResult> WriteAsync<TRequest>(
        DurableJournalWriteOperation operation,
        AuthorizedDurableRequest<TRequest> request,
        DurableOperationAddress address,
        SecurityAuthorizationContext authorization,
        FencingToken fencingToken,
        InputFingerprint fingerprint,
        SecurityEffect effect,
        Func<SqliteConnection, SqliteTransaction, DurableJournalEnforcementReceipt, DurableRecordResult> body,
        CancellationToken cancellationToken)
        where TRequest : class
    {
        Debug.Assert(body is not null, "A write body is required.");
        var started = DurableStorageDiagnostics.TryGetTimestamp(_timeProvider);
        using var activityScope = AgentKitActivityScope.Start(
            AgentKitActivityNames.DurableJournalWrite,
            ActivityKind.Internal,
            new ActivityTagsCollection { { AgentKitTagNames.DurableJournalOperation, operation.ToStableValue() } });
        var activity = activityScope.Activity;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            _database.RequireInitialized("operation journal");
            var (receipt, denial) = await _enforcement.EnforceAsync(
                request,
                address,
                authorization,
                fencingToken,
                fingerprint,
                SecurityOperationKind.StateMutation,
                effect,
                cancellationToken).ConfigureAwait(false);
            if (receipt is not { } enforcement)
            {
                var refused = new DurableRecordFailed(
                    denial ?? "The durable journal operation was not authorized.", committed: false);
                FinishWrite(activity, operation, DurableJournalWriteOutcome.Denied, started, null);
                return refused;
            }

            cancellationToken.ThrowIfCancellationRequested();
            DurableRecordResult result;
            using (var connection = _database.OpenValidated())
            {
                using var transaction = connection.BeginTransaction(deferred: false);
                _database.Validate(connection, requireWal: true, performIntegrityCheck: false, transaction);
                cancellationToken.ThrowIfCancellationRequested();
                result = body(connection, transaction, enforcement);
            }

            FinishWrite(activity, operation, Classify(result), started, null);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            FinishWrite(activity, operation, DurableJournalWriteOutcome.Cancelled, started, nameof(OperationCanceledException));
            throw;
        }
        catch (SqliteException exception)
        {
            FinishWrite(
                activity, operation, DurableJournalWriteOutcome.Failed, started,
                DurableStorageDiagnostics.ErrorType(exception));
            return new DurableRecordFailed(
                "The SQLite durable journal could not complete the write.", committed: null);
        }
        catch (Exception exception)
        {
            FinishWrite(
                activity, operation, DurableJournalWriteOutcome.Failed, started,
                DurableStorageDiagnostics.ErrorType(exception));
            throw;
        }
    }

    private static ActivityTagsCollection CreateTags(DurableOperationAddress address) => new()
    {
        { AgentKitTagNames.AgentId, address.AgentId.ToString() },
        { AgentKitTagNames.SessionId, address.SessionId.ToString() },
        { AgentKitTagNames.RunId, address.RunId.ToString() },
        { AgentKitTagNames.OperationId, address.OperationId.ToString() },
    };

    private static DurableJournalWriteOutcome Classify(DurableRecordResult result)
    {
        Debug.Assert(result is not null, "A terminal write result is required.");
        return result switch
        {
            DurableRecorded => DurableJournalWriteOutcome.Recorded,
            DurableRecordFenced => DurableJournalWriteOutcome.Fenced,
            _ => DurableJournalWriteOutcome.Failed,
        };
    }

    private void FinishWrite(
        Activity? activity,
        DurableJournalWriteOperation operation,
        DurableJournalWriteOutcome outcome,
        long? started,
        string? errorType)
    {
        var operationValue = operation.ToStableValue();
        var outcomeValue = outcome.ToStableValue();
        DurableStorageDiagnostics.SafeSetActivity(activity, current =>
        {
            if (outcome == DurableJournalWriteOutcome.Recorded)
            {
                current.SetSuccessful(outcomeValue);
            }
            else
            {
                current.SetFailed(outcomeValue, errorType ?? outcomeValue);
            }
        });
        DurableStorageDiagnostics.SafeObserve(() =>
        {
            if (errorType is not null && outcome == DurableJournalWriteOutcome.Failed)
            {
                SqliteDurableJournalLog.WriteFailed(_logger, operationValue, errorType);
            }
            else if (errorType is not null && outcome == DurableJournalWriteOutcome.Cancelled)
            {
                SqliteDurableJournalLog.WriteCancelled(_logger, operationValue);
            }
            else if (outcome == DurableJournalWriteOutcome.Denied)
            {
                SqliteDurableJournalLog.WriteDenied(_logger, operationValue);
            }
            else
            {
                SqliteDurableJournalLog.WriteCompleted(_logger, operationValue, outcomeValue);
            }
        });
        var elapsed = DurableStorageDiagnostics.TryGetElapsedTime(_timeProvider, started);
        DurableStorageDiagnostics.SafeObserve(() => DurableJournalMetrics.RecordWrite(operation, outcome, elapsed));
    }

    private void FinishEvidenceLoad(
        Activity? activity,
        DurableJournalEvidenceOutcome outcome,
        long? started,
        string? errorType)
    {
        var outcomeValue = outcome.ToStableValue();
        DurableStorageDiagnostics.SafeSetActivity(activity, current =>
        {
            if (outcome is DurableJournalEvidenceOutcome.Loaded or DurableJournalEvidenceOutcome.NotFound)
            {
                current.SetSuccessful(outcomeValue);
            }
            else
            {
                current.SetFailed(outcomeValue, errorType ?? outcomeValue);
            }
        });
        DurableStorageDiagnostics.SafeObserve(() =>
        {
            if (outcome == DurableJournalEvidenceOutcome.Cancelled)
            {
                SqliteDurableJournalLog.EvidenceLoadCancelled(_logger);
            }
            else
            {
                SqliteDurableJournalLog.EvidenceLoadCompleted(_logger, outcomeValue);
            }
        });
        var elapsed = DurableStorageDiagnostics.TryGetElapsedTime(_timeProvider, started);
        DurableStorageDiagnostics.SafeObserve(() => DurableJournalMetrics.RecordEvidenceLoad(outcome, elapsed));
    }
}
