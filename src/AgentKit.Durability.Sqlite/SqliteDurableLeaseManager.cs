// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Sqlite;

using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Coordinates exclusive ownership of durable operations across processes on one host through SQLite.</summary>
/// <remarks>
/// <para>
/// Fencing tokens are allocated by the database, not by the process: acquisition runs
/// <c>UPDATE durable_lease_sequence SET next_token = next_token + 1 … RETURNING next_token</c> inside the same immediate
/// transaction that installs the lease row, so two processes racing for the same operation receive strictly increasing
/// generations and exactly one of them installs a row. That is the property an in-process counter cannot provide, and
/// it is why the journal must be configured against the same database.
/// </para>
/// <para>
/// Expiry is evaluated only through the injected <see cref="TimeProvider"/>, never an ambient clock or a wall-clock
/// delay, so takeover is deterministic in tests. Acquisition never waits for a busy lease; it returns the current owner
/// and expiry so the caller can decide whether to wait, escalate, or do other work.
/// </para>
/// <para>
/// The claim this adapter makes is host-local multi-process exclusion. SQLite coordinates writers on one machine and
/// provides no distributed lease or cross-host fencing, so composing two hosts against separate database files produces
/// no mutual exclusion at all.
/// </para>
/// </remarks>
public sealed class SqliteDurableLeaseManager: IDurableLeaseManager
{
    private readonly SqliteDurableDatabase _database;
    private readonly IIdentifierGenerator<ExecutionLeaseId> _leaseIds;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SqliteDurableLeaseManager> _logger;

    /// <summary>Initializes a manager over one host-authorized database with replaceable identity and clock collaborators.</summary>
    /// <param name="database">The non-null shared database boundary this manager and its journal commit against.</param>
    /// <param name="leaseIds">The non-null allocator for new lease identities.</param>
    /// <param name="timeProvider">The non-null injected clock used for expiry and elapsed measurement.</param>
    /// <param name="logger">The optional content-free structured logger.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    public SqliteDurableLeaseManager(
        SqliteDurableDatabase database,
        IIdentifierGenerator<ExecutionLeaseId> leaseIds,
        TimeProvider timeProvider,
        ILogger<SqliteDurableLeaseManager>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(leaseIds);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _database = database;
        _leaseIds = leaseIds;
        _timeProvider = timeProvider;
        _logger = logger ?? NullLogger<SqliteDurableLeaseManager>.Instance;
    }

    /// <summary>Creates or validates the exact version-one schema and persistent store identity under bootstrap policy.</summary>
    /// <param name="cancellationToken">Cancels before the schema transaction commits or before a permitted journal-mode change begins.</param>
    /// <returns>A task completed after the exact target is ready for lease operations.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before the applicable bootstrap linearization point.</exception>
    /// <exception cref="InvalidOperationException">The target, schema, store identity, encoding contract, or persistence provider cannot be validated safely.</exception>
    /// <remarks>Call this once during trusted host startup. Initializing the shared database more than once, including from the journal, is a no-op.</remarks>
    public ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        _database.Initialize(cancellationToken);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">The store is uninitialized, persistence failed, or the selected lease-identity generator produced a default identity.</exception>
    public ValueTask<ExecutionLeaseResult> AcquireAsync(
        ExecutionLeaseRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var started = DurableStorageDiagnostics.TryGetTimestamp(_timeProvider);
        using var activityScope = AgentKitActivityScope.Start(
            AgentKitActivityNames.DurableLeaseAcquire,
            ActivityKind.Internal,
            CreateTags(request.Address, request.WorkerId));
        var activity = activityScope.Activity;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            _database.RequireInitialized("lease manager");
            cancellationToken.ThrowIfCancellationRequested();
            var now = _timeProvider.GetUtcNow();
            using var connection = _database.OpenValidated();
            using var transaction = connection.BeginTransaction(deferred: false);
            _database.Validate(connection, requireWal: true, performIntegrityCheck: false, transaction);
            cancellationToken.ThrowIfCancellationRequested();
            var existing = TryReadLease(connection, transaction, request.Address);
            if (existing is { } current && current.ExpiresAt > now)
            {
                transaction.Commit();
                FinishAcquisition(
                    activity, DurableLeaseAcquisitionOutcome.HeldByAnotherWorker, started, null, request.WorkerId);
                return ValueTask.FromResult<ExecutionLeaseResult>(
                    new ExecutionLeaseHeldByAnotherWorker(current.WorkerId, current.FencingToken, current.ExpiresAt));
            }

            var leaseId = _leaseIds.Create();
            if (leaseId == default)
            {
                throw new InvalidOperationException(
                    "The selected lease-identity generator produced a default identity.");
            }

            var token = new FencingToken(AllocateToken(connection, transaction));
            var expiresAt = now + request.Duration;
            InstallLease(connection, transaction, request, leaseId, token, expiresAt);
            cancellationToken.ThrowIfCancellationRequested();
            transaction.Commit();
            var lease = new SqliteExecutionLease(
                this, leaseId, request.WorkerId, request.Address, token, request.Duration, expiresAt);
            FinishAcquisition(
                activity,
                existing is null
                    ? DurableLeaseAcquisitionOutcome.GrantedFirstOwnership
                    : DurableLeaseAcquisitionOutcome.GrantedByTakeover,
                started,
                null,
                request.WorkerId);
            return ValueTask.FromResult<ExecutionLeaseResult>(new ExecutionLeaseAcquired(lease));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            FinishAcquisition(
                activity, DurableLeaseAcquisitionOutcome.Cancelled, started, nameof(OperationCanceledException),
                request.WorkerId);
            throw;
        }
        catch (Exception exception)
        {
            FinishAcquisition(
                activity, DurableLeaseAcquisitionOutcome.Failed, started,
                DurableStorageDiagnostics.ErrorType(exception), request.WorkerId);
            throw;
        }
    }

    /// <summary>Attempts to extend expiry for a lease this manager granted, without changing its generation.</summary>
    /// <param name="lease">The non-null lease requesting renewal.</param>
    /// <param name="cancellationToken">Cancels the renewal attempt; it does not release the lease.</param>
    /// <returns>The terminal renewal outcome.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was signalled before renewal committed.</exception>
    internal ValueTask<LeaseRenewalResult> RenewAsync(SqliteExecutionLease lease, CancellationToken cancellationToken)
    {
        Debug.Assert(lease is not null, "Only this manager's own leases call back into it.");
        var started = DurableStorageDiagnostics.TryGetTimestamp(_timeProvider);
        using var activityScope = AgentKitActivityScope.Start(
            AgentKitActivityNames.DurableLeaseRenew,
            ActivityKind.Internal,
            CreateTags(lease.Address, lease.OwnerWorkerId));
        var activity = activityScope.Activity;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            _database.RequireInitialized("lease manager");
            cancellationToken.ThrowIfCancellationRequested();
            var now = _timeProvider.GetUtcNow();
            using var connection = _database.OpenValidated();
            using var transaction = connection.BeginTransaction(deferred: false);
            _database.Validate(connection, requireWal: true, performIntegrityCheck: false, transaction);
            cancellationToken.ThrowIfCancellationRequested();
            var current = TryReadLease(connection, transaction, lease.Address);

            // Ownership is lost either because another worker's takeover installed a newer generation, or because this
            // lease's own expiry passed before renewal: the store is authoritative for expiry, so a worker that missed
            // its window cannot resurrect ownership by renewing late.
            if (current is not { } row || row.FencingToken != lease.FencingToken || row.ExpiresAt <= now)
            {
                transaction.Commit();
                FinishRenewal(activity, DurableLeaseRenewalOutcome.Lost, started, null, lease.OwnerWorkerId);
                return ValueTask.FromResult<LeaseRenewalResult>(new LeaseLost(current?.FencingToken));
            }

            var expiresAt = now + row.Duration;
            ExtendLease(connection, transaction, lease.Address, lease.FencingToken, expiresAt);
            cancellationToken.ThrowIfCancellationRequested();
            transaction.Commit();
            lease.ExpiresAt = expiresAt;
            FinishRenewal(activity, DurableLeaseRenewalOutcome.Renewed, started, null, lease.OwnerWorkerId);
            return ValueTask.FromResult<LeaseRenewalResult>(new LeaseRenewed(expiresAt));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            FinishRenewal(
                activity, DurableLeaseRenewalOutcome.Cancelled, started, nameof(OperationCanceledException),
                lease.OwnerWorkerId);
            throw;
        }
        catch (Exception exception)
        {
            FinishRenewal(
                activity, DurableLeaseRenewalOutcome.Failed, started,
                DurableStorageDiagnostics.ErrorType(exception), lease.OwnerWorkerId);
            throw;
        }
    }

    /// <summary>Releases ownership for a lease this manager granted, if it is still the current generation.</summary>
    /// <param name="lease">The non-null lease being disposed.</param>
    /// <remarks>
    /// Deleting the row lets another worker acquire immediately instead of waiting for expiry. A lease that already lost
    /// ownership to takeover releases nothing, because the delete is conditioned on its own generation. Release runs
    /// during disposal, so a provider failure is logged and swallowed rather than thrown out of <c>DisposeAsync</c>;
    /// the abandoned row still expires on its own.
    /// </remarks>
    internal void Release(SqliteExecutionLease lease)
    {
        Debug.Assert(lease is not null, "Only this manager's own leases call back into it.");
        using var activityScope = AgentKitActivityScope.Start(
            AgentKitActivityNames.DurableLeaseRelease,
            ActivityKind.Internal,
            CreateTags(lease.Address, lease.OwnerWorkerId));
        var activity = activityScope.Activity;
        try
        {
            using var connection = _database.OpenValidated();
            using var transaction = connection.BeginTransaction(deferred: false);
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
                    DELETE FROM durable_leases
                    WHERE agent_id = $agent AND session_id = $session AND run_id = $run
                      AND operation_id = $operation AND turn_id = $turn AND fencing_token = $token;
                    """;
                SqliteDurableDatabase.EncodeKey(lease.Address).Bind(command);
                _ = command.Parameters.AddWithValue("$token", lease.FencingToken.Value);
                _ = command.ExecuteNonQuery();
            }

            transaction.Commit();
            DurableStorageDiagnostics.SafeSetActivity(activity, static current => current.SetSuccessful("released"));
            DurableStorageDiagnostics.SafeObserve(
                () => SqliteDurableJournalLog.Released(_logger, lease.OwnerWorkerId));
        }
        catch (Exception exception)
        {
            var errorType = DurableStorageDiagnostics.ErrorType(exception);
            DurableStorageDiagnostics.SafeSetActivity(
                activity, current => current.SetFailed("release_failed", errorType));
            DurableStorageDiagnostics.SafeObserve(
                () => SqliteDurableJournalLog.ReleaseFailed(_logger, lease.OwnerWorkerId, errorType));
        }

        DurableStorageDiagnostics.SafeObserve(DurableLeaseMetrics.RecordRelease);
    }

    private static long AllocateToken(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "UPDATE durable_lease_sequence SET next_token = next_token + 1 WHERE id = 1 RETURNING next_token;";
        var allocated = command.ExecuteScalar();
        return allocated is null
            ? throw SqliteDurableDatabase.Unavailable(
                SqliteDurableDatabase.CorruptEvidence,
                "The SQLite durable store has no fencing-token sequence row.")
            : Convert.ToInt64(allocated, CultureInfo.InvariantCulture);
    }

    private static void InstallLease(
        SqliteConnection connection,
        SqliteTransaction transaction,
        ExecutionLeaseRequest request,
        ExecutionLeaseId leaseId,
        FencingToken token,
        DateTimeOffset expiresAt)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO durable_leases
                (agent_id, session_id, run_id, operation_id, turn_id, lease_id, worker_id, fencing_token,
                 duration_ticks, expires_at_ticks, expires_at_offset_ticks)
            VALUES ($agent, $session, $run, $operation, $turn, $lease, $worker, $token,
                    $duration, $expires, $offset)
            ON CONFLICT(agent_id, session_id, run_id, operation_id, turn_id) DO UPDATE SET
                lease_id = excluded.lease_id,
                worker_id = excluded.worker_id,
                fencing_token = excluded.fencing_token,
                duration_ticks = excluded.duration_ticks,
                expires_at_ticks = excluded.expires_at_ticks,
                expires_at_offset_ticks = excluded.expires_at_offset_ticks;
            """;
        SqliteDurableDatabase.EncodeKey(request.Address).Bind(command);
        _ = command.Parameters.AddWithValue("$lease", SqliteDurableCodec.EncodeGuid(leaseId.Value));
        _ = command.Parameters.AddWithValue("$worker", SqliteDurableCodec.EncodeGuid(request.WorkerId.Value));
        _ = command.Parameters.AddWithValue("$token", token.Value);
        _ = command.Parameters.AddWithValue("$duration", request.Duration.Ticks);
        _ = command.Parameters.AddWithValue("$expires", expiresAt.UtcTicks);
        _ = command.Parameters.AddWithValue("$offset", expiresAt.Offset.Ticks);
        if (command.ExecuteNonQuery() != 1)
        {
            throw SqliteDurableDatabase.Unavailable(
                SqliteDurableDatabase.PersistenceFailed,
                "The SQLite execution lease could not be installed atomically.");
        }
    }

    private static void ExtendLease(
        SqliteConnection connection,
        SqliteTransaction transaction,
        DurableOperationAddress address,
        FencingToken token,
        DateTimeOffset expiresAt)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE durable_leases
            SET expires_at_ticks = $expires, expires_at_offset_ticks = $offset
            WHERE agent_id = $agent AND session_id = $session AND run_id = $run
              AND operation_id = $operation AND turn_id = $turn AND fencing_token = $token;
            """;
        SqliteDurableDatabase.EncodeKey(address).Bind(command);
        _ = command.Parameters.AddWithValue("$token", token.Value);
        _ = command.Parameters.AddWithValue("$expires", expiresAt.UtcTicks);
        _ = command.Parameters.AddWithValue("$offset", expiresAt.Offset.Ticks);
        if (command.ExecuteNonQuery() != 1)
        {
            throw SqliteDurableDatabase.Unavailable(
                SqliteDurableDatabase.PersistenceFailed,
                "The SQLite execution lease renewal could not be committed atomically.");
        }
    }

    private static PersistedLease? TryReadLease(
        SqliteConnection connection,
        SqliteTransaction transaction,
        DurableOperationAddress address)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT worker_id, fencing_token, duration_ticks, expires_at_ticks, expires_at_offset_ticks
            FROM durable_leases
            WHERE agent_id = $agent AND session_id = $session AND run_id = $run
              AND operation_id = $operation AND turn_id = $turn;
            """;
        SqliteDurableDatabase.EncodeKey(address).Bind(command);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        var workerId = new WorkerId(SqliteDurableCodec.DecodeGuid(SqliteDurableDatabase.ReadBoundedBlob(reader, 0, 16)));
        return new PersistedLease(
            workerId,
            new FencingToken(reader.GetInt64(1)),
            TimeSpan.FromTicks(reader.GetInt64(2)),
            new DateTimeOffset(reader.GetInt64(3), TimeSpan.Zero).ToOffset(TimeSpan.FromTicks(reader.GetInt64(4))));
    }

    private static ActivityTagsCollection CreateTags(DurableOperationAddress address, WorkerId workerId) => new()
    {
        { AgentKitTagNames.AgentId, address.AgentId.ToString() },
        { AgentKitTagNames.SessionId, address.SessionId.ToString() },
        { AgentKitTagNames.RunId, address.RunId.ToString() },
        { AgentKitTagNames.OperationId, address.OperationId.ToString() },
        { AgentKitTagNames.WorkerId, workerId.ToString() },
    };

    private void FinishAcquisition(
        Activity? activity,
        DurableLeaseAcquisitionOutcome outcome,
        long? started,
        string? errorType,
        WorkerId workerId)
    {
        var outcomeValue = outcome.ToStableValue();
        var granted = outcome
            is DurableLeaseAcquisitionOutcome.GrantedFirstOwnership
            or DurableLeaseAcquisitionOutcome.GrantedByTakeover;
        DurableStorageDiagnostics.SafeSetActivity(activity, current =>
        {
            if (granted)
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
            if (errorType is not null && outcome == DurableLeaseAcquisitionOutcome.Failed)
            {
                SqliteDurableJournalLog.AcquisitionFailed(_logger, workerId, errorType);
            }
            else if (outcome == DurableLeaseAcquisitionOutcome.Cancelled)
            {
                SqliteDurableJournalLog.AcquisitionCancelled(_logger, workerId);
            }
            else
            {
                SqliteDurableJournalLog.AcquisitionCompleted(_logger, workerId, outcomeValue);
            }
        });
        var elapsed = DurableStorageDiagnostics.TryGetElapsedTime(_timeProvider, started);
        DurableStorageDiagnostics.SafeObserve(() => DurableLeaseMetrics.RecordAcquisition(outcome, elapsed));
    }

    private void FinishRenewal(
        Activity? activity,
        DurableLeaseRenewalOutcome outcome,
        long? started,
        string? errorType,
        WorkerId workerId)
    {
        var outcomeValue = outcome.ToStableValue();
        DurableStorageDiagnostics.SafeSetActivity(activity, current =>
        {
            if (outcome == DurableLeaseRenewalOutcome.Renewed)
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
            if (errorType is not null && outcome == DurableLeaseRenewalOutcome.Failed)
            {
                SqliteDurableJournalLog.RenewalFailed(_logger, workerId, errorType);
            }
            else if (outcome == DurableLeaseRenewalOutcome.Cancelled)
            {
                SqliteDurableJournalLog.RenewalCancelled(_logger, workerId);
            }
            else
            {
                SqliteDurableJournalLog.RenewalCompleted(_logger, workerId, outcomeValue);
            }
        });
        var elapsed = DurableStorageDiagnostics.TryGetElapsedTime(_timeProvider, started);
        DurableStorageDiagnostics.SafeObserve(() => DurableLeaseMetrics.RecordRenewal(outcome, elapsed));
    }

    private readonly record struct PersistedLease(
        WorkerId WorkerId,
        FencingToken FencingToken,
        TimeSpan Duration,
        DateTimeOffset ExpiresAt);
}
