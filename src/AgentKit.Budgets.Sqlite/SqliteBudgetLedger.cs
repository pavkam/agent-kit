// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Sqlite;

/// <summary>Persists authoritative budget topology, reservations, accounting, replay, and recovery evidence in one fixed SQLite target.</summary>
/// <remarks>Call <see cref="InitializeAsync"/> during trusted host bootstrap before ordinary operations.</remarks>
public sealed class SqliteBudgetLedger: IBudgetLedger
{
    private static readonly BudgetLedgerDescriptor _descriptor = new(true, BudgetLedgerConcurrencyDomain.HostLocal);
    private readonly SqliteBudgetLedgerDatabase _database;
    private readonly SqliteBudgetLedgerSettings _settings;
    private readonly IIdentifierGenerator<BudgetScopeId> _scopeIds;
    private readonly IIdentifierGenerator<BudgetReservationId> _reservationIds;
    private readonly IBudgetDimensionCatalog _dimensions;
    private readonly TimeProvider _timeProvider;
    private readonly SqliteBudgetLedgerObservation _observation;

    /// <summary>Creates a durable host-local ledger without opening its configured target.</summary>
    /// <param name="target">The exact trusted-bootstrap target.</param>
    /// <param name="settings">The immutable transaction and codec bounds.</param>
    /// <param name="timeProvider">The deterministic accounting clock.</param>
    /// <param name="scopeIds">The source of new scope identities.</param>
    /// <param name="reservationIds">The source of new reservation identities.</param>
    /// <param name="dimensions">The captured dimension semantics catalog.</param><param name="logger">The optional observer logger.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public SqliteBudgetLedger(SqliteBudgetLedgerTarget target, SqliteBudgetLedgerSettings settings, TimeProvider timeProvider, IIdentifierGenerator<BudgetScopeId> scopeIds, IIdentifierGenerator<BudgetReservationId> reservationIds, IBudgetDimensionCatalog dimensions, ILogger<SqliteBudgetLedger>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(scopeIds);
        ArgumentNullException.ThrowIfNull(reservationIds);
        ArgumentNullException.ThrowIfNull(dimensions);
        _database = new(target, settings);
        _settings = settings;
        _timeProvider = timeProvider;
        _scopeIds = scopeIds;
        _reservationIds = reservationIds;
        _dimensions = dimensions;
        _observation = new(timeProvider, logger ?? NullLogger<SqliteBudgetLedger>.Instance);
    }

    /// <inheritdoc/>
    public BudgetLedgerDescriptor Descriptor => _descriptor;

    /// <summary>Creates or validates the exact version-one database during trusted bootstrap.</summary>
    /// <param name="cancellationToken">Cancels before bootstrap linearizes.</param>
    /// <returns>A completed task after the target is ready.</returns>
    /// <remarks>If schema commit succeeds but WAL establishment or final validation fails, retrying the same fixed target validates and completes the known initialization rather than creating different state.</remarks>
    public ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        _ = _observation.Run("initialize", () => { _database.Initialize(cancellationToken); return true; }, static _ => "succeeded");
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<BudgetLedgerScopeCreateResult> CreateScopeAsync(BudgetLedgerScopeCreateRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = _observation.Run("create_scope", () => _database.Write((connection, transaction) => CreateScopeCore(connection, transaction, request, cancellationToken), cancellationToken), ScopeOutcome, request.OriginalRequest.Address,
            establishedScopeSelector: static outcome => outcome is BudgetLedgerScopeCreated created ? created.Scope.Id : null);
        return ValueTask.FromResult(result);
    }

    private BudgetLedgerScopeCreateResult CreateScopeCore(SqliteConnection connection, SqliteTransaction transaction, BudgetLedgerScopeCreateRequest request, CancellationToken cancellationToken)
    {
        Debug.Assert(connection is not null && transaction is not null && request is not null, "Validated transaction inputs are required.");
        var replay = ReadScopeByKey(connection, transaction, request.OriginalRequest.IdempotencyKey);
        var replayResult = BudgetScopeCreateTransition.Replay(request, replay?.Request, replay?.Reference);
        if (replayResult is not null)
        {
            return replayResult;
        }
        if (request.OriginalRequest.Limits.Length > _settings.MaximumLimitsPerScope)
        {
            throw new BudgetLedgerStateException("The scope exceeds the adapter's captured limit-evidence bound.");
        }
        SqliteScopeRow? parent = null;
        if (request.OriginalRequest.ParentScopeId is { } parentId)
        {
            parent = ReadScope(connection, transaction, parentId);
            if (parent is null || !IsParentAddress(parent.Reference.Address, request.OriginalRequest.Address))
            {
                throw new BudgetLedgerReferenceUnavailableException("The parent scope is unavailable.");
            }
        }
        var lineage = parent is null ? [] : LoadLineage(connection, transaction, parent);
        var parentEvidence = parent is null ? null : new ScopeCreateParent(
            parent.Reference, parent.Request, parent.Depth, [.. lineage.Select(static ancestor => ancestor.Request)]);
        if (parent is not null && parent.Depth >= _settings.MaximumLineageDepth)
        {
            return new BudgetLedgerScopeCreateRejected(new(
                BudgetScopeCreationFailureKind.MaximumDepthExceeded,
                "The scope would exceed the adapter's configured lineage bound."));
        }
        var rejection = BudgetScopeCreateTransition.EvaluateDepth(request, parentEvidence);
        if (rejection is not null)
        {
            return rejection;
        }
        var descriptors = request.OriginalRequest.Limits.Select(limit => _dimensions.TryGet(limit.Dimension, out var descriptor) ? descriptor : null).ToImmutableArray();
        rejection = BudgetScopeCreateTransition.EvaluateLimits(request, parentEvidence, descriptors);
        if (rejection is not null)
        {
            return rejection;
        }
        var id = _scopeIds.Create();
        ArgumentOutOfRangeException.ThrowIfEqual(id, default, nameof(id));
        var revision = ReadRevision(connection, transaction);
        var (result, mutation) = BudgetScopeCreateTransition.PlanAccepted(request, parentEvidence, id, ReadScope(connection, transaction, id) is not null, checked(revision + 1));
        cancellationToken.ThrowIfCancellationRequested();
        var payload = SqliteBudgetLedgerCodec.Encode(request, _settings, _settings.MaximumPayloadBytes);
        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "INSERT INTO budget_scopes(scope_id,parent_scope_id,depth,request,request_digest) VALUES($id,$parent,$depth,$request,$digest); INSERT INTO budget_scope_keys(idempotency_key,scope_id) VALUES($key,$id); UPDATE budget_ledger_metadata SET revision=$revision;";
            _ = command.Parameters.AddWithValue("$id", id.Value.ToByteArray());
            _ = command.Parameters.AddWithValue("$parent", mutation.ParentScopeId is { } parentScopeId ? parentScopeId.Value.ToByteArray() : DBNull.Value);
            _ = command.Parameters.AddWithValue("$depth", mutation.Depth);
            _ = command.Parameters.AddWithValue("$request", payload);
            _ = command.Parameters.AddWithValue("$digest", SHA256.HashData(payload));
            _ = command.Parameters.AddWithValue("$key", request.OriginalRequest.IdempotencyKey.Value);
            _ = command.Parameters.AddWithValue("$revision", mutation.Revision);
            _ = command.ExecuteNonQuery();
        }
        return result;
    }

    private SqliteScopeRow? ReadScopeByKey(SqliteConnection connection, SqliteTransaction transaction, IdempotencyKey key)
    {
        Debug.Assert(connection is not null && transaction is not null, "An active adapter-owned transaction is required.");
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT s.scope_id,s.parent_scope_id,s.depth,s.request,s.request_digest FROM budget_scope_keys k JOIN budget_scopes s ON s.scope_id=k.scope_id WHERE k.idempotency_key=$key;";
        _ = command.Parameters.AddWithValue("$key", key.Value);
        return ReadSqliteScopeRow(command);
    }

    private SqliteScopeRow? ReadScope(SqliteConnection connection, SqliteTransaction transaction, BudgetScopeId id)
    {
        Debug.Assert(connection is not null && transaction is not null, "An active adapter-owned transaction is required.");
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT scope_id,parent_scope_id,depth,request,request_digest FROM budget_scopes WHERE scope_id=$id;";
        _ = command.Parameters.AddWithValue("$id", id.Value.ToByteArray());
        return ReadSqliteScopeRow(command);
    }

    private SqliteScopeRow? ReadSqliteScopeRow(SqliteCommand command)
    {
        Debug.Assert(command is not null, "A configured scope query is required.");
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadStored(() => ReadSqliteScopeRow(reader)) : null;
    }

    private SqliteScopeRow ReadSqliteScopeRow(SqliteDataReader reader)
    {
        Debug.Assert(reader is not null, "An active persisted scope row is required.");
        var payload = ReadBoundedBlob(reader, 3, _settings.MaximumPayloadBytes);
        var digest = ReadBoundedBlob(reader, 4, SHA256.HashSizeInBytes);
        if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(payload), digest))
        {
            throw new InvalidDataException("Persisted scope evidence failed its integrity check.");
        }
        var request = SqliteBudgetLedgerCodec.Decode<BudgetLedgerScopeCreateRequest>(payload, _settings, _settings.MaximumPayloadBytes);
        var id = new BudgetScopeId(new Guid(ReadBoundedBlob(reader, 0, 16)));
        BudgetScopeId? parentId = reader.IsDBNull(1) ? null : new BudgetScopeId(new Guid(ReadBoundedBlob(reader, 1, 16)));
        return parentId != request.OriginalRequest.ParentScopeId
            ? throw new InvalidDataException("Persisted scope parent projection differs from its immutable evidence.")
            : new(new BudgetLedgerScopeReference(id, request.OriginalRequest.Address), request, parentId, reader.GetInt32(2));
    }

    private static long ReadRevision(SqliteConnection connection, SqliteTransaction transaction)
    {
        Debug.Assert(connection is not null && transaction is not null, "An active adapter-owned transaction is required.");
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT revision FROM budget_ledger_metadata;";
        return ReadStored(() => Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture));
    }

    private SqliteScopeRow RequireScope(SqliteConnection connection, SqliteTransaction transaction, BudgetLedgerScopeReference reference)
    {
        Debug.Assert(connection is not null && transaction is not null && reference is not null, "Validated scope lookup inputs are required.");
        var scope = ReadScope(connection, transaction, reference.Id);
        return scope is null || scope.Reference != reference
            ? throw new BudgetLedgerReferenceUnavailableException("The budget scope is unavailable.")
            : scope;
    }

    private ImmutableArray<SqliteScopeRow> LoadLineage(SqliteConnection connection, SqliteTransaction transaction, SqliteScopeRow scope)
    {
        Debug.Assert(connection is not null && transaction is not null && scope is not null, "Validated lineage inputs are required.");
        var builder = ImmutableArray.CreateBuilder<SqliteScopeRow>();
        var visited = new HashSet<BudgetScopeId>();
        for (var current = scope; ;)
        {
            if (!visited.Add(current.Reference.Id) || builder.Count >= _settings.MaximumLineageDepth)
            {
                throw new InvalidDataException("Persisted budget scope lineage is cyclic or exceeds its configured bound.");
            }
            builder.Add(current);
            if (current.ParentId is not { } parentId)
            {
                if (current.Depth != 1)
                {
                    throw new InvalidDataException("Persisted budget root depth is inconsistent.");
                }
                break;
            }
            var parent = ReadScope(connection, transaction, parentId)
                ?? throw new InvalidDataException("Persisted budget scope lineage references a missing parent.");
            if (parent.Depth != current.Depth - 1)
            {
                throw new InvalidDataException("Persisted budget scope lineage depth is inconsistent.");
            }
            current = parent;
        }
        return builder.ToImmutable();
    }

    private List<SqliteReservationRow> ReadCapacityReservations(SqliteConnection connection, SqliteTransaction transaction, BudgetScopeId scopeId)
    {
        Debug.Assert(connection is not null && transaction is not null, "An active adapter-owned transaction is required.");
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT r.receipt,r.receipt_digest,r.aggregation,r.started_ticks,r.started_offset_ticks,r.start_revision,r.released,r.start_expiration,r.start_expiration_digest,r.original_commit,r.original_commit_digest,r.current_commit,r.current_commit_digest,r.accounting_revision,r.latest_correction_revision FROM budget_reservation_charges c JOIN budget_reservations r ON r.reservation_id=c.reservation_id WHERE c.scope_id=$scope AND c.active=1;";
        _ = command.Parameters.AddWithValue("$scope", scopeId.Value.ToByteArray());
        using var reader = command.ExecuteReader();
        var rows = new List<SqliteReservationRow>();
        while (reader.Read())
        {
            rows.Add(ReadReservationRow(reader));
        }
        return rows;
    }

    private SqliteReservationRow RequireReservation(SqliteConnection connection, SqliteTransaction transaction, BudgetLedgerReservationReference reference)
    {
        Debug.Assert(connection is not null && transaction is not null && reference is not null, "Validated reservation lookup inputs are required.");
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT receipt,receipt_digest,aggregation,started_ticks,started_offset_ticks,start_revision,released,start_expiration,start_expiration_digest,original_commit,original_commit_digest,current_commit,current_commit_digest,accounting_revision,latest_correction_revision FROM budget_reservations WHERE reservation_id=$id;";
        _ = command.Parameters.AddWithValue("$id", reference.Id.Value.ToByteArray());
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new BudgetLedgerReferenceUnavailableException("The budget reservation is unavailable.");
        }
        var row = ReadReservationRow(reader);
        return row.Receipt.Reservation != reference
            ? throw new BudgetLedgerReferenceUnavailableException("The budget reservation is unavailable.")
            : row;
    }

    private SqliteReservationRow ReadReservationRow(SqliteDataReader reader) => ReadStored(() =>
    {
        var receipt = DecodeVerified<BudgetLedgerReservationReceipt>(reader, 0, 1, _settings.MaximumResultBytes);
        DateTimeOffset? startedAt = reader.IsDBNull(3) ? null : new DateTimeOffset(reader.GetInt64(3), TimeSpan.FromTicks(reader.GetInt64(4)));
        var expiration = reader.IsDBNull(7) ? null : DecodeVerified<BudgetStartExpired>(reader, 7, 8, _settings.MaximumResultBytes);
        var originalCommit = reader.IsDBNull(9) ? null : DecodeVerified<BudgetCommitResult>(reader, 9, 10, _settings.MaximumResultBytes);
        var currentCommit = reader.IsDBNull(11) ? null : DecodeVerified<BudgetCommitResult>(reader, 11, 12, _settings.MaximumResultBytes);
        BudgetAccountingRevision? accountingRevision = reader.IsDBNull(13) ? null : new(reader.GetInt64(13));
        return new SqliteReservationRow(receipt, (BudgetAggregationKind) reader.GetInt32(2), startedAt, reader.GetInt64(5), reader.GetBoolean(6), expiration, originalCommit, currentCommit, accountingRevision, reader.GetInt64(14));
    });

    private ImmutableArray<BudgetOverrunHold> ReadActiveHolds(SqliteConnection connection, SqliteTransaction transaction, BudgetScopeId scopeId)
    {
        Debug.Assert(connection is not null && transaction is not null, "An active adapter-owned transaction is required.");
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT evidence,evidence_digest FROM budget_overrun_holds WHERE boundary_scope_id=$scope AND automatically_cleared=0 AND resolution IS NULL ORDER BY accounting_revision,reservation_id;";
        _ = command.Parameters.AddWithValue("$scope", scopeId.Value.ToByteArray());
        using var reader = command.ExecuteReader();
        var holds = ImmutableArray.CreateBuilder<BudgetOverrunHold>();
        while (reader.Read())
        {
            holds.Add(DecodeVerified<BudgetOverrunHold>(reader, 0, 1, _settings.MaximumResultBytes));
        }
        return holds.ToImmutable();
    }

    private T DecodeVerified<T>(SqliteDataReader reader, int payloadOrdinal, int digestOrdinal, int maximumBytes)
    {
        Debug.Assert(reader is not null, "An active persisted row reader is required.");
        var payload = ReadBoundedBlob(reader, payloadOrdinal, maximumBytes);
        var digest = ReadBoundedBlob(reader, digestOrdinal, SHA256.HashSizeInBytes);
        return digest.Length != SHA256.HashSizeInBytes || !CryptographicOperations.FixedTimeEquals(SHA256.HashData(payload), digest)
            ? throw new InvalidDataException("Persisted budget evidence failed its integrity check.")
            : SqliteBudgetLedgerCodec.Decode<T>(payload, _settings, maximumBytes);
    }

    private static byte[] ReadBoundedBlob(SqliteDataReader reader, int ordinal, int maximumBytes)
    {
        Debug.Assert(reader is not null, "An active persisted row reader is required.");
        var length = reader.GetBytes(ordinal, 0, null, 0, 0);
        if (length <= 0 || length > maximumBytes)
        {
            throw new InvalidDataException("Persisted budget evidence exceeds its configured bound.");
        }
        var payload = new byte[checked((int) length)];
        _ = reader.GetBytes(ordinal, 0, payload, 0, payload.Length);
        return payload;
    }

    private static bool BatchKeyExists(SqliteConnection connection, SqliteTransaction transaction, IdempotencyKey key)
    {
        Debug.Assert(connection is not null && transaction is not null, "An active adapter-owned transaction is required.");
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT 1 FROM budget_batch_keys WHERE idempotency_key=$key;";
        _ = command.Parameters.AddWithValue("$key", key.Value);
        return command.ExecuteScalar() is not null;
    }

    private static bool ReservationExists(SqliteConnection connection, SqliteTransaction transaction, BudgetReservationId id)
    {
        Debug.Assert(connection is not null && transaction is not null, "An active adapter-owned transaction is required.");
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT 1 FROM budget_reservations WHERE reservation_id=$id;";
        _ = command.Parameters.AddWithValue("$id", id.Value.ToByteArray());
        return command.ExecuteScalar() is not null;
    }

    private void InsertReservation(SqliteConnection connection, SqliteTransaction transaction, BudgetLedgerReservationReceipt receipt, BudgetAggregationKind aggregation, ImmutableArray<SqliteScopeRow> lineage)
    {
        Debug.Assert(connection is not null && transaction is not null && receipt is not null, "Validated reservation persistence inputs are required.");
        Debug.Assert(!lineage.IsDefaultOrEmpty, "A validated charged lineage is required.");
        var payload = SqliteBudgetLedgerCodec.Encode(receipt, _settings, _settings.MaximumResultBytes);
        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "INSERT INTO budget_reservations(reservation_id,scope_id,receipt,receipt_digest,aggregation,start_revision,released,latest_correction_revision) VALUES($id,$scope,$receipt,$digest,$aggregation,0,0,0);";
            _ = command.Parameters.AddWithValue("$id", receipt.Reservation.Id.Value.ToByteArray());
            _ = command.Parameters.AddWithValue("$scope", receipt.Reservation.Scope.Id.Value.ToByteArray());
            _ = command.Parameters.AddWithValue("$receipt", payload);
            _ = command.Parameters.AddWithValue("$digest", SHA256.HashData(payload));
            _ = command.Parameters.AddWithValue("$aggregation", (int) aggregation);
            _ = command.ExecuteNonQuery();
        }
        foreach (var boundary in lineage)
        {
            using var charge = connection.CreateCommand();
            charge.Transaction = transaction;
            charge.CommandText = "INSERT INTO budget_reservation_charges(scope_id,reservation_id,active) VALUES($scope,$reservation,1);";
            _ = charge.Parameters.AddWithValue("$scope", boundary.Reference.Id.Value.ToByteArray());
            _ = charge.Parameters.AddWithValue("$reservation", receipt.Reservation.Id.Value.ToByteArray());
            _ = charge.ExecuteNonQuery();
            ApplyProjectionDelta(connection, transaction, boundary.Reference.Id,
                receipt.OriginalRequest.Dimension, receipt.OriginalRequest.Unit, aggregation,
                receipt.OriginalRequest.Amount, 0, 0, 0, 1);
        }
    }

    private void PersistExpiration(SqliteConnection connection, SqliteTransaction transaction, SqliteReservationRow row)
    {
        Debug.Assert(connection is not null && transaction is not null && row is not null, "Validated expiration inputs are required.");
        var expiration = new BudgetStartExpired(row.Receipt.Reservation.Id, row.Receipt.EffectiveReservation.ExpiresAt);
        var payload = SqliteBudgetLedgerCodec.Encode(expiration, _settings, _settings.MaximumResultBytes);
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "UPDATE budget_reservations SET released=1,start_expiration=$expiration,start_expiration_digest=$digest WHERE reservation_id=$id;";
        _ = command.Parameters.AddWithValue("$expiration", payload);
        _ = command.Parameters.AddWithValue("$digest", SHA256.HashData(payload));
        _ = command.Parameters.AddWithValue("$id", row.Receipt.Reservation.Id.Value.ToByteArray());
        _ = command.ExecuteNonQuery();
        DeactivateCharges(connection, transaction, row.Receipt.Reservation.Id);
        ApplyCapacityRelease(connection, transaction, row);
    }

    private void ApplyCapacityRelease(SqliteConnection connection, SqliteTransaction transaction, SqliteReservationRow row)
    {
        Debug.Assert(connection is not null && transaction is not null && row is not null, "Validated capacity-release inputs are required.");
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT scope_id FROM budget_reservation_charges WHERE reservation_id=$reservation;";
        _ = command.Parameters.AddWithValue("$reservation", row.Receipt.Reservation.Id.Value.ToByteArray());
        using var reader = command.ExecuteReader();
        var scopes = new List<BudgetScopeId>();
        while (reader.Read())
        {
            scopes.Add(new(new Guid(ReadBoundedBlob(reader, 0, 16))));
        }
        reader.Close();
        foreach (var scopeId in scopes)
        {
            ApplyProjectionDelta(connection, transaction, scopeId, row.Receipt.OriginalRequest.Dimension,
                row.Receipt.OriginalRequest.Unit, row.Aggregation, 0, row.Receipt.OriginalRequest.Amount, 0, 0, -1);
        }
    }

    private static void DeactivateCharges(SqliteConnection connection, SqliteTransaction transaction, BudgetReservationId reservationId)
    {
        Debug.Assert(connection is not null && transaction is not null, "An active adapter-owned transaction is required.");
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "UPDATE budget_reservation_charges SET active=0 WHERE reservation_id=$reservation;";
        _ = command.Parameters.AddWithValue("$reservation", reservationId.Value.ToByteArray());
        _ = command.ExecuteNonQuery();
    }

    private static void SetRevision(SqliteConnection connection, SqliteTransaction transaction, long revision)
    {
        Debug.Assert(connection is not null && transaction is not null, "An active adapter-owned transaction is required.");
        Debug.Assert(revision > 0, "A committed positive ledger revision is required.");
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "UPDATE budget_ledger_metadata SET revision=$revision;";
        _ = command.Parameters.AddWithValue("$revision", revision);
        _ = command.ExecuteNonQuery();
    }

    private static BudgetQuantity Sum(IEnumerable<decimal> values)
    {
        Debug.Assert(values is not null, "A validated amount sequence is required.");
        var result = default(BudgetQuantity);
        foreach (var value in values)
        {
            result = result.Add(BudgetQuantity.FromDecimal(value));
        }
        return result;
    }

    private static BudgetQuantity Max(BudgetQuantity left, BudgetQuantity right) => left.CompareTo(right) >= 0 ? left : right;

    private SqliteDimensionProjection? ReadProjection(SqliteConnection connection, SqliteTransaction transaction, BudgetScopeId scopeId, BudgetDimension dimension)
    {
        Debug.Assert(connection is not null && transaction is not null, "An active adapter-owned transaction is required.");
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT unit,aggregation,reserved_coefficient,reserved_scale,committed_coefficient,committed_scale,open_count FROM budget_dimension_projections WHERE scope_id=$scope AND dimension=$dimension;";
        _ = command.Parameters.AddWithValue("$scope", scopeId.Value.ToByteArray());
        _ = command.Parameters.AddWithValue("$dimension", dimension.Value);
        using var reader = command.ExecuteReader();
        return !reader.Read() ? null : ReadStored(() =>
        {
            var reserved = ReadQuantity(reader, 2, 3);
            var committed = ReadQuantity(reader, 4, 5);
            return new SqliteDimensionProjection(dimension, new(reader.GetString(0)), (BudgetAggregationKind) reader.GetInt32(1), reserved, committed, reader.GetInt32(6));
        });
    }

    private ImmutableArray<SqliteDimensionProjection> ReadProjections(SqliteConnection connection, SqliteTransaction transaction, BudgetScopeId scopeId)
    {
        Debug.Assert(connection is not null && transaction is not null, "An active adapter-owned transaction is required.");
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT dimension,unit,aggregation,reserved_coefficient,reserved_scale,committed_coefficient,committed_scale,open_count FROM budget_dimension_projections WHERE scope_id=$scope ORDER BY dimension;";
        _ = command.Parameters.AddWithValue("$scope", scopeId.Value.ToByteArray());
        using var reader = command.ExecuteReader();
        var result = ImmutableArray.CreateBuilder<SqliteDimensionProjection>();
        while (reader.Read())
        {
            result.Add(ReadStored(() => new SqliteDimensionProjection(new(reader.GetString(0)), new(reader.GetString(1)),
                (BudgetAggregationKind) reader.GetInt32(2), ReadQuantity(reader, 3, 4), ReadQuantity(reader, 5, 6), reader.GetInt32(7))));
        }
        return result.ToImmutable();
    }

    private BudgetQuantity ReadQuantity(SqliteDataReader reader, int coefficientOrdinal, int scaleOrdinal)
    {
        Debug.Assert(reader is not null, "An active persisted row reader is required.");
        var length = reader.GetBytes(coefficientOrdinal, 0, null, 0, 0);
        if (length <= 0 || length > _settings.MaximumResultBytes)
        {
            throw new InvalidDataException("Persisted exact budget quantity has an invalid bounded length.");
        }
        var bytes = new byte[checked((int) length)];
        _ = reader.GetBytes(coefficientOrdinal, 0, bytes, 0, bytes.Length);
        return new(new BigInteger(bytes, isUnsigned: true, isBigEndian: true), reader.GetInt32(scaleOrdinal));
    }

    private static T ReadStored<T>(Func<T> read)
    {
        Debug.Assert(read is not null, "A persisted-value reader is required.");
        Debug.Assert(read is not null, "A trusted internal stored-value reader is required.");
        try
        {
            return read();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidCastException or OverflowException)
        {
            throw new InvalidDataException("Persisted budget projections contain invalid typed values.", exception);
        }
    }

    private byte[] QuantityBytes(BudgetQuantity value)
    {
        var length = value.Coefficient.GetByteCount(isUnsigned: true);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(length, _settings.MaximumResultBytes, nameof(value));
        var bytes = new byte[Math.Max(length, 1)];
        _ = value.Coefficient.TryWriteBytes(bytes, out _, isUnsigned: true, isBigEndian: true);
        return bytes;
    }

    private static BudgetQuantity Subtract(BudgetQuantity value, decimal amount)
    {
        var right = BudgetQuantity.FromDecimal(amount);
        var scale = Math.Max(value.Scale, right.Scale);
        var coefficient = (value.Coefficient * BigInteger.Pow(10, scale - value.Scale))
            - (right.Coefficient * BigInteger.Pow(10, scale - right.Scale));
        return coefficient.Sign < 0
            ? throw new InvalidDataException("Persisted budget projection would become negative.")
            : new(coefficient, scale);
    }

    private void ApplyProjectionDelta(SqliteConnection connection, SqliteTransaction transaction, BudgetScopeId scopeId,
        BudgetDimension dimension, BudgetUnit unit, BudgetAggregationKind aggregation, decimal reservedAdd, decimal reservedSubtract,
        decimal committedAdd, decimal committedSubtract, int openDelta)
    {
        Debug.Assert(connection is not null && transaction is not null, "An active adapter-owned transaction is required.");
        var current = ReadProjection(connection, transaction, scopeId, dimension);
        if (current is not null && (current.Unit != unit || current.Aggregation != aggregation))
        {
            throw new InvalidDataException("Persisted budget projection semantics conflict with immutable reservation evidence.");
        }
        var reserved = current?.Reserved ?? default;
        var committed = current?.Committed ?? default;
        var openCount = checked((current?.OpenCount ?? 0) + openDelta);
        if (openCount < 0)
        {
            throw new InvalidDataException("Persisted budget projection row count would become negative.");
        }
        if (aggregation == BudgetAggregationKind.Maximum)
        {
            UpdateMaximumValue(connection, transaction, scopeId, dimension, reservedAdd, liveDelta: reservedAdd > 0 ? 1 : 0, committedDelta: 0);
            UpdateMaximumValue(connection, transaction, scopeId, dimension, reservedSubtract, liveDelta: reservedSubtract > 0 ? -1 : 0, committedDelta: 0);
            UpdateMaximumValue(connection, transaction, scopeId, dimension, committedAdd, liveDelta: 0, committedDelta: committedAdd > 0 ? 1 : 0);
            UpdateMaximumValue(connection, transaction, scopeId, dimension, committedSubtract, liveDelta: 0, committedDelta: committedSubtract > 0 ? -1 : 0);
            (reserved, committed) = ReadMaximumProjection(connection, transaction, scopeId, dimension);
        }
        else
        {
            reserved = Subtract(reserved.Add(BudgetQuantity.FromDecimal(reservedAdd)), reservedSubtract);
            committed = aggregation == BudgetAggregationKind.ConcurrentGauge
                ? default
                : Subtract(committed.Add(BudgetQuantity.FromDecimal(committedAdd)), committedSubtract);
        }
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO budget_dimension_projections(scope_id,dimension,unit,aggregation,reserved_coefficient,reserved_scale,committed_coefficient,committed_scale,open_count) VALUES($scope,$dimension,$unit,$aggregation,$reserved,$reserved_scale,$committed,$committed_scale,$open) ON CONFLICT(scope_id,dimension) DO UPDATE SET reserved_coefficient=$reserved,reserved_scale=$reserved_scale,committed_coefficient=$committed,committed_scale=$committed_scale,open_count=$open;";
        _ = command.Parameters.AddWithValue("$scope", scopeId.Value.ToByteArray());
        _ = command.Parameters.AddWithValue("$dimension", dimension.Value);
        _ = command.Parameters.AddWithValue("$unit", unit.Value);
        _ = command.Parameters.AddWithValue("$aggregation", (int) aggregation);
        _ = command.Parameters.AddWithValue("$reserved", QuantityBytes(reserved));
        _ = command.Parameters.AddWithValue("$reserved_scale", reserved.Scale);
        _ = command.Parameters.AddWithValue("$committed", QuantityBytes(committed));
        _ = command.Parameters.AddWithValue("$committed_scale", committed.Scale);
        _ = command.Parameters.AddWithValue("$open", openCount);
        _ = command.ExecuteNonQuery();
    }

    private static void UpdateMaximumValue(SqliteConnection connection, SqliteTransaction transaction, BudgetScopeId scopeId,
        BudgetDimension dimension, decimal amount, int liveDelta, int committedDelta)
    {
        Debug.Assert(connection is not null && transaction is not null, "An active adapter-owned transaction is required.");
        if (amount == 0 || (liveDelta == 0 && committedDelta == 0))
        {
            return;
        }
        var quantity = BudgetQuantity.FromDecimal(amount);
        var key = MaximumKey(amount);
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = liveDelta < 0 || committedDelta < 0
            ? "UPDATE budget_maximum_values SET live_count=live_count+$live,committed_count=committed_count+$committed WHERE scope_id=$scope AND dimension=$dimension AND amount_key=$key; DELETE FROM budget_maximum_values WHERE scope_id=$scope AND dimension=$dimension AND amount_key=$key AND live_count=0 AND committed_count=0;"
            : "INSERT INTO budget_maximum_values(scope_id,dimension,amount_key,amount_text,live_count,committed_count) VALUES($scope,$dimension,$key,$text,$live,$committed) ON CONFLICT(scope_id,dimension,amount_key) DO UPDATE SET live_count=live_count+$live,committed_count=committed_count+$committed;";
        _ = command.Parameters.AddWithValue("$scope", scopeId.Value.ToByteArray());
        _ = command.Parameters.AddWithValue("$dimension", dimension.Value);
        _ = command.Parameters.AddWithValue("$key", key);
        _ = command.Parameters.AddWithValue("$text", quantity.ToString());
        _ = command.Parameters.AddWithValue("$live", liveDelta);
        _ = command.Parameters.AddWithValue("$committed", committedDelta);
        var affected = command.ExecuteNonQuery();
        if ((liveDelta < 0 || committedDelta < 0) && affected == 0)
        {
            throw new InvalidDataException("Persisted maximum accounting is missing the decremented value.");
        }
    }

    /// <summary>Encodes one nonnegative decimal into a fixed-width key whose byte ordering equals numeric ordering.</summary>
    /// <param name="amount">The nonnegative exact decimal value.</param>
    /// <returns>A new 24-byte unsigned big-endian scale-28 key.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="amount"/> is negative.</exception>
    internal static byte[] MaximumKey(decimal amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        var quantity = BudgetQuantity.FromDecimal(amount);
        var scaled = quantity.Coefficient * BigInteger.Pow(10, 28 - quantity.Scale);
        var key = new byte[24];
        var byteCount = scaled.GetByteCount(isUnsigned: true);
        return byteCount <= key.Length
            && scaled.TryWriteBytes(key.AsSpan(key.Length - byteCount), out _, isUnsigned: true, isBigEndian: true)
                ? key
                : throw new InvalidDataException("A decimal maximum cannot be represented by the fixed sortable key.");
    }

    private static (BudgetQuantity Reserved, BudgetQuantity Committed) ReadMaximumProjection(SqliteConnection connection, SqliteTransaction transaction, BudgetScopeId scopeId, BudgetDimension dimension)
    {
        Debug.Assert(connection is not null && transaction is not null, "An active adapter-owned transaction is required.");
        BudgetQuantity Read(string predicate)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"SELECT amount_key,amount_text,length(CAST(amount_text AS BLOB)) FROM budget_maximum_values WHERE scope_id=$scope AND dimension=$dimension AND {predicate}>0 ORDER BY amount_key DESC LIMIT 1;";
            _ = command.Parameters.AddWithValue("$scope", scopeId.Value.ToByteArray());
            _ = command.Parameters.AddWithValue("$dimension", dimension.Value);
            using var reader = command.ExecuteReader();
            return reader.Read() ? ReadMaximumValue(reader) : default;
        }
        return (Read("live_count"), Read("committed_count"));
    }

    private static BudgetOverrunHold HoldEvidence(SqliteScopeRow boundary, SqliteReservationRow reservation, BudgetAccountingRevision revision, decimal actual)
    {
        Debug.Assert(boundary is not null && reservation is not null, "Validated hold evidence inputs are required.");
        var original = reservation.Receipt.OriginalRequest;
        return new(new BudgetOverrunHoldReference(boundary.Reference, reservation.Receipt.Reservation, revision),
            original.Dimension, original.Unit, original.Amount, actual, boundary.Request.Admission.OverrunHoldPolicy);
    }

    private void InsertHold(SqliteConnection connection, SqliteTransaction transaction, BudgetOverrunHold hold)
    {
        Debug.Assert(connection is not null && transaction is not null && hold is not null, "Validated hold persistence inputs are required.");
        var payload = SqliteBudgetLedgerCodec.Encode(hold, _settings, _settings.MaximumResultBytes);
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO budget_overrun_holds(boundary_scope_id,reservation_id,accounting_revision,evidence,evidence_digest,automatically_cleared) VALUES($boundary,$reservation,$revision,$evidence,$digest,0);";
        _ = command.Parameters.AddWithValue("$boundary", hold.Reference.Boundary.Id.Value.ToByteArray());
        _ = command.Parameters.AddWithValue("$reservation", hold.Reference.Reservation.Id.Value.ToByteArray());
        _ = command.Parameters.AddWithValue("$revision", hold.Reference.TriggeringRevision.Value);
        _ = command.Parameters.AddWithValue("$evidence", payload);
        _ = command.Parameters.AddWithValue("$digest", SHA256.HashData(payload));
        _ = command.ExecuteNonQuery();
    }

    private BudgetCorrectionResult? ReadCorrection(SqliteConnection connection, SqliteTransaction transaction, BudgetReservationId reservationId, long revision)
    {
        Debug.Assert(connection is not null && transaction is not null, "An active adapter-owned transaction is required.");
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT result,result_digest FROM budget_corrections WHERE reservation_id=$id AND correction_revision=$revision;";
        _ = command.Parameters.AddWithValue("$id", reservationId.Value.ToByteArray());
        _ = command.Parameters.AddWithValue("$revision", revision);
        using var reader = command.ExecuteReader();
        return reader.Read() ? DecodeVerified<BudgetCorrectionResult>(reader, 0, 1, _settings.MaximumResultBytes) : null;
    }

    private bool EligibleForClear(SqliteConnection connection, SqliteTransaction transaction, SqliteScopeRow boundary, SqliteReservationRow correcting, decimal correctedActual)
    {
        Debug.Assert(connection is not null && transaction is not null && boundary is not null && correcting is not null, "Validated hold-clear inputs are required.");
        var dimension = correcting.Receipt.OriginalRequest.Dimension;
        var stillOverrun = ReadActiveHolds(connection, transaction, boundary.Reference.Id)
            .Where(hold => hold.Dimension == dimension)
            .Any(hold =>
            {
                var row = RequireReservation(connection, transaction, hold.Reference.Reservation);
                var actual = row.Receipt.Reservation == correcting.Receipt.Reservation ? correctedActual : row.CurrentCommit?.Actual ?? hold.CurrentActual;
                return actual > row.Receipt.OriginalRequest.Amount;
            });
        if (stillOverrun)
        {
            return false;
        }
        var hard = boundary.Request.OriginalRequest.Limits.FirstOrDefault(limit => limit.Dimension == dimension && limit.Kind == BudgetLimitKind.Hard);
        if (hard is null)
        {
            return true;
        }
        var projection = ReadProjection(connection, transaction, boundary.Reference.Id, dimension)
            ?? throw new InvalidDataException("Settled accounting has no dimension projection.");
        var committed = correcting.Aggregation == BudgetAggregationKind.Maximum
            ? MaximumAfterReplacement(connection, transaction, boundary.Reference.Id, dimension, correcting.CurrentCommit!.Actual, correctedActual)
            : Subtract(projection.Committed, correcting.CurrentCommit!.Actual).Add(BudgetQuantity.FromDecimal(correctedActual));
        var observed = correcting.Aggregation switch
        {
            BudgetAggregationKind.Maximum => Max(projection.Reserved, committed),
            BudgetAggregationKind.ConcurrentGauge => projection.Reserved,
            BudgetAggregationKind.Sum or BudgetAggregationKind.Duration => projection.Reserved.Add(committed),
            _ => throw new BudgetLedgerStateException("The corrected accounting has unsupported aggregation semantics."),
        };
        return observed.CompareTo(BudgetQuantity.FromDecimal(hard.Value)) <= 0;
    }

    private static BudgetQuantity MaximumAfterReplacement(SqliteConnection connection, SqliteTransaction transaction, BudgetScopeId scopeId,
        BudgetDimension dimension, decimal previous, decimal corrected)
    {
        var key = MaximumKey(previous);
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT amount_key,amount_text,length(CAST(amount_text AS BLOB)) FROM budget_maximum_values WHERE scope_id=$scope AND dimension=$dimension AND committed_count > 0 AND (amount_key <> $key OR committed_count > 1) ORDER BY amount_key DESC LIMIT 1;";
        _ = command.Parameters.AddWithValue("$scope", scopeId.Value.ToByteArray());
        _ = command.Parameters.AddWithValue("$dimension", dimension.Value);
        _ = command.Parameters.AddWithValue("$key", key);
        using var reader = command.ExecuteReader();
        var remaining = reader.Read() ? ReadMaximumValue(reader) : default;
        return Max(remaining, BudgetQuantity.FromDecimal(corrected));
    }

    private static BudgetQuantity ReadMaximumValue(SqliteDataReader reader) => ReadStored(() =>
    {
        var textLength = reader.GetInt32(2);
        if (textLength is <= 0 or > 64)
        {
            throw new InvalidDataException("Persisted maximum text has an invalid bounded length.");
        }
        var text = reader.GetString(1);
        if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) || amount < 0)
        {
            throw new InvalidDataException("Persisted maximum text is not a nonnegative decimal.");
        }
        var quantity = BudgetQuantity.FromDecimal(amount);
        var key = ReadBoundedBlob(reader, 0, 24);
        return quantity.ToString() == text && key.AsSpan().SequenceEqual(MaximumKey(amount))
            ? quantity
            : throw new InvalidDataException("Persisted maximum text and sortable key disagree.");
    });

    private static void SetHoldCleared(SqliteConnection connection, SqliteTransaction transaction, BudgetOverrunHoldReference reference)
    {
        Debug.Assert(connection is not null && transaction is not null && reference is not null, "Validated hold-clear inputs are required.");
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "UPDATE budget_overrun_holds SET automatically_cleared=1 WHERE boundary_scope_id=$boundary AND reservation_id=$reservation AND accounting_revision=$revision;";
        _ = command.Parameters.AddWithValue("$boundary", reference.Boundary.Id.Value.ToByteArray());
        _ = command.Parameters.AddWithValue("$reservation", reference.Reservation.Id.Value.ToByteArray());
        _ = command.Parameters.AddWithValue("$revision", reference.TriggeringRevision.Value);
        _ = command.ExecuteNonQuery();
    }

    private BudgetLedgerReconciliationReleased ReleaseReconciled(SqliteConnection connection, SqliteTransaction transaction, SqliteReservationRow row)
    {
        Debug.Assert(connection is not null && transaction is not null && row is not null, "Validated reconciliation inputs are required.");
        var revision = checked(ReadRevision(connection, transaction) + 1);
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "UPDATE budget_reservations SET released=1 WHERE reservation_id=$id; UPDATE budget_ledger_metadata SET revision=$revision;";
        _ = command.Parameters.AddWithValue("$id", row.Receipt.Reservation.Id.Value.ToByteArray());
        _ = command.Parameters.AddWithValue("$revision", revision);
        _ = command.ExecuteNonQuery();
        DeactivateCharges(connection, transaction, row.Receipt.Reservation.Id);
        ApplyCapacityRelease(connection, transaction, row);
        return new(row.Receipt.Reservation);
    }

    private static bool IsParentAddress(BudgetScopeAddress parent, BudgetScopeAddress child) => parent.TenantId == child.TenantId && parent.PrincipalId == child.PrincipalId && parent.AgentId == child.AgentId && (parent.SessionId is null || parent.SessionId == child.SessionId) && (parent.RunId is null || parent.RunId == child.RunId) && (parent.OperationId is null || parent.OperationId == child.OperationId);

    /// <inheritdoc/>
    public ValueTask<BudgetLedgerBatchReserveResult> ReserveBatchAsync(BudgetLedgerBatchReserveRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = _observation.Run("reserve_batch", () => _database.Write((connection, transaction) => ReserveBatchCore(connection, transaction, request, cancellationToken), cancellationToken), ReserveOutcome, request.Scope.Address, request.Scope.Id,
            operationId: request.OriginalRequests[0].OperationId);
        return ValueTask.FromResult(result);
    }

    private BudgetLedgerBatchReserveResult ReserveBatchCore(SqliteConnection connection, SqliteTransaction transaction, BudgetLedgerBatchReserveRequest request, CancellationToken cancellationToken)
    {
        Debug.Assert(connection is not null && transaction is not null && request is not null, "Validated reservation inputs are required.");
        var scope = RequireScope(connection, transaction, request.Scope);
        BudgetLedgerBatchReserveRequest? replayRequest = null;
        BudgetLedgerBatchReserved? replayResult = null;
        foreach (var item in request.OriginalRequests)
        {
            using var replay = connection.CreateCommand();
            replay.Transaction = transaction;
            replay.CommandText = "SELECT request,request_digest,result,result_digest FROM budget_batch_keys WHERE idempotency_key=$key;";
            _ = replay.Parameters.AddWithValue("$key", item.IdempotencyKey.Value);
            using var reader = replay.ExecuteReader();
            if (!reader.Read())
            {
                continue;
            }
            var foundRequest = DecodeVerified<BudgetLedgerBatchReserveRequest>(reader, 0, 1, _settings.MaximumPayloadBytes);
            var foundResult = DecodeVerified<BudgetLedgerBatchReserved>(reader, 2, 3, _settings.MaximumResultBytes);
            if (replayRequest is not null && (replayRequest != foundRequest || replayResult != foundResult))
            {
                throw new BudgetLedgerMutationConflictException("Batch item keys belong to different batches.");
            }
            replayRequest = foundRequest;
            replayResult = foundResult;
        }
        if (replayRequest is not null)
        {
            return replayRequest != request || request.OriginalRequests.Any(item => !BatchKeyExists(connection, transaction, item.IdempotencyKey))
                ? throw new BudgetLedgerMutationConflictException("A batch item key is bound to different ordered evidence.")
                : replayResult!;
        }
        if (request.OriginalRequests.Length > _settings.MaximumBatchSize)
        {
            throw new BudgetLedgerStateException("The atomic batch exceeds the adapter's captured reservation bound.");
        }
        var descriptors = ImmutableArray.CreateBuilder<BudgetDimensionDescriptor>(request.OriginalRequests.Length);
        foreach (var item in request.OriginalRequests)
        {
            if (!_dimensions.TryGet(item.Dimension, out var descriptor) || !descriptor.AllowedUnits.Contains(item.Unit) || !Enum.IsDefined(descriptor.Aggregation))
            {
                throw new BudgetLedgerStateException("The reservation has no compatible dimension descriptor.");
            }
            if (request.OriginalRequests.Any(candidate => candidate.Dimension == item.Dimension && candidate.Unit != item.Unit))
            {
                throw new BudgetLedgerStateException("One atomic batch cannot mix units for the same dimension.");
            }
            descriptors.Add(descriptor);
        }
        var lineage = LoadLineage(connection, transaction, scope);
        var rowsByBoundary = lineage.ToDictionary(boundary => boundary.Reference.Id, boundary => ReadCapacityReservations(connection, transaction, boundary.Reference.Id));
        foreach (var boundary in lineage)
        {
            using (var projectionCount = connection.CreateCommand())
            {
                projectionCount.Transaction = transaction;
                projectionCount.CommandText = "SELECT COUNT(*) FROM budget_dimension_projections WHERE scope_id=$scope;";
                _ = projectionCount.Parameters.AddWithValue("$scope", boundary.Reference.Id.Value.ToByteArray());
                var existingCount = Convert.ToInt32(projectionCount.ExecuteScalar(), CultureInfo.InvariantCulture);
                var newCount = request.OriginalRequests.Select(static item => item.Dimension).Distinct()
                    .Count(dimension => ReadProjection(connection, transaction, boundary.Reference.Id, dimension) is null);
                if (existingCount + newCount > _settings.MaximumLimitsPerScope)
                {
                    throw new BudgetLedgerStateException("The atomic batch would exceed the adapter's captured dimension-projection bound.");
                }
            }
            foreach (var item in request.OriginalRequests)
            {
                var projection = ReadProjection(connection, transaction, boundary.Reference.Id, item.Dimension);
                var descriptor = descriptors.First(candidate => candidate.Dimension == item.Dimension);
                if (projection is not null && (projection.Unit != item.Unit || projection.Aggregation != descriptor.Aggregation))
                {
                    throw new BudgetLedgerStateException("The reservation descriptor conflicts with existing accounting at a charged boundary.");
                }
            }
        }
        var requestedDimensions = request.OriginalRequests.Select(static item => item.Dimension).ToHashSet();
        var holds = lineage.SelectMany(boundary => ReadActiveHolds(connection, transaction, boundary.Reference.Id))
            .Where(hold => requestedDimensions.Contains(hold.Dimension)).ToImmutableArray();
        if (!holds.IsEmpty)
        {
            return new BudgetLedgerBatchReserveHeld(holds);
        }
        var now = _timeProvider.GetUtcNow();
        var expired = rowsByBoundary.Values.SelectMany(static rows => rows).DistinctBy(static row => row.Receipt.Reservation.Id)
            .Where(row => row.IsCapacityRetaining && row.StartedAt is null && now >= row.Receipt.EffectiveReservation.ExpiresAt).ToArray();
        foreach (var boundary in lineage)
        {
            var boundaryRows = rowsByBoundary[boundary.Reference.Id];
            var open = boundaryRows.Count(row => !expired.Contains(row));
            if (open + request.OriginalRequests.Length > boundary.Request.Admission.MaximumOpenReservationsPerScope)
            {
                throw new BudgetLedgerStateException("The atomic batch would exceed the scope's captured open-reservation capacity.");
            }
            foreach (var group in request.OriginalRequests.GroupBy(static item => (item.Dimension, item.Unit)))
            {
                var limit = boundary.Request.OriginalRequest.Limits.FirstOrDefault(item => item.Dimension == group.Key.Dimension);
                if (limit is not null && limit.Unit != group.Key.Unit)
                {
                    throw new BudgetLedgerStateException("The reservation unit conflicts with a captured scope limit.");
                }
                var descriptor = descriptors.First(item => item.Dimension == group.Key.Dimension);
                var projection = ReadProjection(connection, transaction, boundary.Reference.Id, group.Key.Dimension);
                var reserved = projection?.Reserved ?? default;
                if (descriptor.Aggregation == BudgetAggregationKind.Maximum)
                {
                    reserved = MaximumRetained(boundaryRows, expired, group.Key.Dimension);
                }
                else
                {
                    foreach (var expiredRow in expired.Where(row => row.Receipt.OriginalRequest.Dimension == group.Key.Dimension
                        && boundaryRows.Contains(row)))
                    {
                        reserved = Subtract(reserved, expiredRow.Receipt.OriginalRequest.Amount);
                    }
                }
                var committed = projection?.Committed ?? default;
                var observed = descriptor.Aggregation == BudgetAggregationKind.Maximum ? Max(reserved, committed) : reserved.Add(committed);
                var amount = descriptor.Aggregation == BudgetAggregationKind.Maximum
                    ? BudgetQuantity.FromDecimal(group.Max(static item => item.Amount))
                    : Sum(group.Select(static item => item.Amount));
                var projected = descriptor.Aggregation == BudgetAggregationKind.Maximum ? Max(observed, amount) : observed.Add(amount);
                if (limit is { Kind: BudgetLimitKind.Hard } && projected.CompareTo(BudgetQuantity.FromDecimal(limit.Value)) > 0)
                {
                    return new BudgetLedgerBatchReserveRejected(new BudgetLimitFailure(boundary.Reference.Id, group.Key.Dimension,
                        BudgetLimitKind.Hard, limit.Value, observed, amount, limit.Unit, "The atomic reservation batch would exceed the captured hard limit."));
                }
            }
        }
        var receipts = ImmutableArray.CreateBuilder<BudgetLedgerReservationReceipt>(request.OriginalRequests.Length);
        var ids = new HashSet<BudgetReservationId>();
        foreach (var item in request.OriginalRequests)
        {
            var id = _reservationIds.Create();
            ArgumentOutOfRangeException.ThrowIfEqual(id, default, nameof(id));
            if (!ids.Add(id) || ReservationExists(connection, transaction, id))
            {
                throw new BudgetLedgerStateException("The reservation identity source produced a duplicate value.");
            }
            receipts.Add(new(new BudgetLedgerReservationReference(scope.Reference, id), item,
                new BudgetEffectiveReservation(item.ExpiresAt ?? (now + scope.Request.Admission.DefaultReservationLifetime))));
        }
        var accepted = new BudgetLedgerBatchReserved(receipts.MoveToImmutable());
        var revision = ReadRevision(connection, transaction);
        _ = checked(revision + expired.Length + 1);
        var requestPayload = SqliteBudgetLedgerCodec.Encode(request, _settings, _settings.MaximumPayloadBytes);
        var resultPayload = SqliteBudgetLedgerCodec.Encode(accepted, _settings, _settings.MaximumResultBytes);
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var expiredRow in expired)
        {
            revision++;
            PersistExpiration(connection, transaction, expiredRow);
        }
        for (var index = 0; index < accepted.Receipts.Length; index++)
        {
            InsertReservation(connection, transaction, accepted.Receipts[index], descriptors[index].Aggregation, lineage);
        }
        foreach (var item in request.OriginalRequests)
        {
            using var key = connection.CreateCommand();
            key.Transaction = transaction;
            key.CommandText = "INSERT INTO budget_batch_keys(idempotency_key,request,request_digest,result,result_digest) VALUES($key,$request,$request_digest,$result,$result_digest);";
            _ = key.Parameters.AddWithValue("$key", item.IdempotencyKey.Value);
            _ = key.Parameters.AddWithValue("$request", requestPayload);
            _ = key.Parameters.AddWithValue("$request_digest", SHA256.HashData(requestPayload));
            _ = key.Parameters.AddWithValue("$result", resultPayload);
            _ = key.Parameters.AddWithValue("$result_digest", SHA256.HashData(resultPayload));
            _ = key.ExecuteNonQuery();
        }
        SetRevision(connection, transaction, revision + 1);
        return accepted;
    }
    /// <inheritdoc/>
    public ValueTask<BudgetStartResult> MarkStartedAsync(BudgetLedgerReservationReference reservation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        var result = _observation.Run("mark_started", () => _database.Write((connection, transaction) => MarkStartedCore(connection, transaction, reservation, cancellationToken), cancellationToken), StartOutcome, reservation.Scope.Address, reservation.Scope.Id, reservation.Id);
        return ValueTask.FromResult(result);
    }

    private BudgetStartResult MarkStartedCore(SqliteConnection connection, SqliteTransaction transaction, BudgetLedgerReservationReference reservation, CancellationToken cancellationToken)
    {
        Debug.Assert(connection is not null && transaction is not null && reservation is not null, "Validated start inputs are required.");
        var row = RequireReservation(connection, transaction, reservation);
        if (row.CurrentCommit is not null || row.Released)
        {
            return row.StartExpiration is not null
                ? row.StartExpiration
                : throw new BudgetLedgerStateException("The reservation is terminal and cannot start.");
        }
        if (row.StartedAt is not null)
        {
            return new BudgetStarted(reservation.Id, true);
        }
        var now = _timeProvider.GetUtcNow();
        var revision = checked(ReadRevision(connection, transaction) + 1);
        cancellationToken.ThrowIfCancellationRequested();
        if (now >= row.Receipt.EffectiveReservation.ExpiresAt)
        {
            PersistExpiration(connection, transaction, row);
            SetRevision(connection, transaction, revision);
            return new BudgetStartExpired(reservation.Id, row.Receipt.EffectiveReservation.ExpiresAt);
        }
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "UPDATE budget_reservations SET started_ticks=$ticks,started_offset_ticks=$offset,start_revision=$revision WHERE reservation_id=$id; UPDATE budget_ledger_metadata SET revision=$revision;";
        _ = command.Parameters.AddWithValue("$ticks", now.Ticks);
        _ = command.Parameters.AddWithValue("$offset", now.Offset.Ticks);
        _ = command.Parameters.AddWithValue("$revision", revision);
        _ = command.Parameters.AddWithValue("$id", reservation.Id.Value.ToByteArray());
        _ = command.ExecuteNonQuery();
        return new BudgetStarted(reservation.Id, false);
    }
    /// <inheritdoc/>
    public ValueTask<BudgetCommitResult> SettleAsync(BudgetLedgerSettlementRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = _observation.Run("settle", () => _database.Write((connection, transaction) => SettleCore(connection, transaction, request, cancellationToken), cancellationToken), static _ => "settled", request.Reservation.Scope.Address, request.Reservation.Scope.Id, request.Reservation.Id);
        return ValueTask.FromResult(result);
    }

    private BudgetCommitResult SettleCore(SqliteConnection connection, SqliteTransaction transaction, BudgetLedgerSettlementRequest request, CancellationToken cancellationToken)
    {
        Debug.Assert(connection is not null && transaction is not null && request is not null, "Validated settlement inputs are required.");
        var row = RequireReservation(connection, transaction, request.Reservation);
        if (row.OriginalCommit is not null)
        {
            return row.OriginalCommit.Actual != request.Actual
                ? throw new BudgetLedgerMutationConflictException("The reservation is settled with different actual usage.")
                : row.OriginalCommit;
        }
        if (row.StartedAt is null || row.Released)
        {
            throw new BudgetLedgerStateException("The reservation is not started or is released.");
        }
        var accountingRevision = new BudgetAccountingRevision(checked(ReadRevision(connection, transaction) + 1));
        var lineage = LoadLineage(connection, transaction, RequireScope(connection, transaction, request.Reservation.Scope));
        var holds = request.Actual > row.Receipt.OriginalRequest.Amount
            ? lineage.Select(boundary => HoldEvidence(boundary, row, accountingRevision, request.Actual)).ToImmutableArray()
            : [];
        var reserved = row.Receipt.OriginalRequest.Amount;
        var commit = new BudgetCommitResult(request.Reservation.Id, reserved, request.Actual,
            Math.Max(0, reserved - request.Actual), Math.Max(0, request.Actual - reserved), accountingRevision, holds);
        var payload = SqliteBudgetLedgerCodec.Encode(commit, _settings, _settings.MaximumResultBytes);
        cancellationToken.ThrowIfCancellationRequested();
        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "UPDATE budget_reservations SET original_commit=$commit,original_commit_digest=$digest,current_commit=$commit,current_commit_digest=$digest,accounting_revision=$revision WHERE reservation_id=$id; UPDATE budget_ledger_metadata SET revision=$revision;";
            _ = command.Parameters.AddWithValue("$commit", payload);
            _ = command.Parameters.AddWithValue("$digest", SHA256.HashData(payload));
            _ = command.Parameters.AddWithValue("$revision", accountingRevision.Value);
            _ = command.Parameters.AddWithValue("$id", request.Reservation.Id.Value.ToByteArray());
            _ = command.ExecuteNonQuery();
        }
        DeactivateCharges(connection, transaction, row.Receipt.Reservation.Id);
        ApplySettlementProjection(connection, transaction, row, request.Actual);
        foreach (var hold in holds)
        {
            InsertHold(connection, transaction, hold);
        }
        return commit;
    }

    private void ApplySettlementProjection(SqliteConnection connection, SqliteTransaction transaction, SqliteReservationRow row, decimal actual)
    {
        Debug.Assert(connection is not null && transaction is not null && row is not null, "Validated settlement projection inputs are required.");
        foreach (var boundary in LoadLineage(connection, transaction, RequireScope(connection, transaction, row.Receipt.Reservation.Scope)))
        {
            ApplyProjectionDelta(connection, transaction, boundary.Reference.Id, row.Receipt.OriginalRequest.Dimension,
                row.Receipt.OriginalRequest.Unit, row.Aggregation, 0, row.Receipt.OriginalRequest.Amount,
                actual, 0, -1);
        }
    }
    /// <inheritdoc/>
    public ValueTask<BudgetLedgerReleaseResult> ReleaseUnstartedAsync(BudgetLedgerReservationReference reservation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        var result = _observation.Run("release", () => _database.Write((connection, transaction) => ReleaseCore(connection, transaction, reservation, cancellationToken), cancellationToken), ReleaseOutcome, reservation.Scope.Address, reservation.Scope.Id, reservation.Id);
        return ValueTask.FromResult(result);
    }

    private BudgetLedgerReleaseResult ReleaseCore(SqliteConnection connection, SqliteTransaction transaction, BudgetLedgerReservationReference reservation, CancellationToken cancellationToken)
    {
        Debug.Assert(connection is not null && transaction is not null && reservation is not null, "Validated release inputs are required.");
        var row = RequireReservation(connection, transaction, reservation);
        if (row.CurrentCommit is not null)
        {
            return new BudgetLedgerAlreadySettled(row.CurrentCommit);
        }
        if (row.StartedAt is not null)
        {
            return new BudgetLedgerRetainedStarted(reservation);
        }
        if (!row.Released)
        {
            var revision = checked(ReadRevision(connection, transaction) + 1);
            cancellationToken.ThrowIfCancellationRequested();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "UPDATE budget_reservations SET released=1 WHERE reservation_id=$id; UPDATE budget_ledger_metadata SET revision=$revision;";
            _ = command.Parameters.AddWithValue("$id", reservation.Id.Value.ToByteArray());
            _ = command.Parameters.AddWithValue("$revision", revision);
            _ = command.ExecuteNonQuery();
            DeactivateCharges(connection, transaction, row.Receipt.Reservation.Id);
            ApplyCapacityRelease(connection, transaction, row);
        }
        return new BudgetLedgerReleased(reservation);
    }
    /// <inheritdoc/>
    public ValueTask<BudgetCorrectionResult> CorrectAsync(BudgetLedgerCorrectionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = _observation.Run("correct", () => _database.Write((connection, transaction) => CorrectCore(connection, transaction, request, cancellationToken), cancellationToken), static _ => "corrected", request.Reservation.Scope.Address, request.Reservation.Scope.Id, request.Reservation.Id);
        return ValueTask.FromResult(result);
    }

    private BudgetCorrectionResult CorrectCore(SqliteConnection connection, SqliteTransaction transaction, BudgetLedgerCorrectionRequest request, CancellationToken cancellationToken)
    {
        Debug.Assert(connection is not null && transaction is not null && request is not null, "Validated correction inputs are required.");
        var row = RequireReservation(connection, transaction, request.Reservation);
        if (row.CurrentCommit is null)
        {
            throw new BudgetLedgerStateException("Only settled accounting can be corrected.");
        }
        var replay = ReadCorrection(connection, transaction, request.Reservation.Id, request.Revision);
        if (replay is not null)
        {
            return replay.CorrectedActual != request.CorrectedActual
                ? throw new BudgetLedgerMutationConflictException("The correction revision is bound to different usage.")
                : replay;
        }
        if (request.Revision <= row.LatestCorrectionRevision)
        {
            throw new BudgetLedgerStateException("Correction revisions must increase monotonically.");
        }
        var accountingRevision = new BudgetAccountingRevision(checked(ReadRevision(connection, transaction) + 1));
        var lineage = LoadLineage(connection, transaction, RequireScope(connection, transaction, request.Reservation.Scope));
        var created = ImmutableArray.CreateBuilder<BudgetOverrunHold>();
        if (row.CurrentCommit.Actual <= row.Receipt.OriginalRequest.Amount && request.CorrectedActual > row.Receipt.OriginalRequest.Amount)
        {
            foreach (var boundary in lineage)
            {
                if (!ReadActiveHolds(connection, transaction, boundary.Reference.Id).Any(hold => hold.Reference.Reservation == request.Reservation))
                {
                    created.Add(HoldEvidence(boundary, row, accountingRevision, request.CorrectedActual));
                }
            }
        }
        var cleared = ImmutableArray.CreateBuilder<BudgetOverrunHoldReference>();
        foreach (var boundary in lineage)
        {
            if (EligibleForClear(connection, transaction, boundary, row, request.CorrectedActual))
            {
                cleared.AddRange(ReadActiveHolds(connection, transaction, boundary.Reference.Id)
                    .Where(hold => hold.Policy == BudgetOverrunHoldPolicy.ClearWhenReconciled && hold.Dimension == row.Receipt.OriginalRequest.Dimension)
                    .Select(static hold => hold.Reference));
            }
        }
        var result = new BudgetCorrectionResult(request.Reservation.Id, row.CurrentCommit.Actual, request.CorrectedActual, request.Revision,
            accountingRevision, created.ToImmutable(), cleared.ToImmutable());
        var commit = new BudgetCommitResult(request.Reservation.Id, row.Receipt.OriginalRequest.Amount, request.CorrectedActual,
            Math.Max(0, row.Receipt.OriginalRequest.Amount - request.CorrectedActual), Math.Max(0, request.CorrectedActual - row.Receipt.OriginalRequest.Amount), accountingRevision, created.ToImmutable());
        var resultPayload = SqliteBudgetLedgerCodec.Encode(result, _settings, _settings.MaximumResultBytes);
        var commitPayload = SqliteBudgetLedgerCodec.Encode(commit, _settings, _settings.MaximumResultBytes);
        cancellationToken.ThrowIfCancellationRequested();
        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "UPDATE budget_reservations SET current_commit=$commit,current_commit_digest=$commit_digest,accounting_revision=$accounting_revision,latest_correction_revision=$correction_revision WHERE reservation_id=$id; INSERT INTO budget_corrections(reservation_id,correction_revision,result,result_digest) VALUES($id,$correction_revision,$result,$result_digest); UPDATE budget_ledger_metadata SET revision=$accounting_revision;";
            _ = command.Parameters.AddWithValue("$commit", commitPayload);
            _ = command.Parameters.AddWithValue("$commit_digest", SHA256.HashData(commitPayload));
            _ = command.Parameters.AddWithValue("$accounting_revision", accountingRevision.Value);
            _ = command.Parameters.AddWithValue("$correction_revision", request.Revision);
            _ = command.Parameters.AddWithValue("$id", request.Reservation.Id.Value.ToByteArray());
            _ = command.Parameters.AddWithValue("$result", resultPayload);
            _ = command.Parameters.AddWithValue("$result_digest", SHA256.HashData(resultPayload));
            _ = command.ExecuteNonQuery();
        }
        foreach (var boundary in lineage)
        {
            ApplyProjectionDelta(connection, transaction, boundary.Reference.Id, row.Receipt.OriginalRequest.Dimension,
                row.Receipt.OriginalRequest.Unit, row.Aggregation, 0, 0,
                request.CorrectedActual, row.CurrentCommit.Actual, 0);
        }
        foreach (var hold in created)
        {
            InsertHold(connection, transaction, hold);
        }
        foreach (var reference in cleared)
        {
            SetHoldCleared(connection, transaction, reference);
        }
        return result;
    }
    /// <inheritdoc/>
    public ValueTask<BudgetSnapshot> GetSnapshotAsync(BudgetLedgerScopeReference scope, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var result = _observation.Run("snapshot", () => _database.Write((connection, transaction) => SnapshotCore(connection, transaction, scope, cancellationToken), cancellationToken), static _ => "succeeded", scope.Address, scope.Id);
        return ValueTask.FromResult(result);
    }

    private BudgetSnapshot SnapshotCore(SqliteConnection connection, SqliteTransaction transaction, BudgetLedgerScopeReference reference, CancellationToken cancellationToken)
    {
        Debug.Assert(connection is not null && transaction is not null && reference is not null, "Validated snapshot inputs are required.");
        var scope = RequireScope(connection, transaction, reference);
        var now = _timeProvider.GetUtcNow();
        var rows = ReadCapacityReservations(connection, transaction, scope.Reference.Id);
        var expired = rows.Where(row => row.IsCapacityRetaining && row.StartedAt is null && now >= row.Receipt.EffectiveReservation.ExpiresAt).ToArray();
        var projections = ReadProjections(connection, transaction, scope.Reference.Id);
        var dimensions = scope.Request.OriginalRequest.Limits.Select(static item => item.Dimension)
            .Concat(projections.Select(static projection => projection.Dimension)).Distinct();
        var usages = dimensions.Select(dimension =>
        {
            var limit = scope.Request.OriginalRequest.Limits.FirstOrDefault(item => item.Dimension == dimension);
            var projection = projections.FirstOrDefault(item => item.Dimension == dimension);
            var unit = limit?.Unit ?? projection!.Unit;
            var reserved = projection?.Reserved ?? default;
            if (projection?.Aggregation == BudgetAggregationKind.Maximum)
            {
                reserved = MaximumRetained(rows, expired, dimension);
            }
            else
            {
                foreach (var expiredRow in expired.Where(row => row.Receipt.OriginalRequest.Dimension == dimension))
                {
                    reserved = Subtract(reserved, expiredRow.Receipt.OriginalRequest.Amount);
                }
            }
            return new BudgetDimensionUsage(dimension, unit, reserved, projection?.Committed ?? default, limit);
        }).ToImmutableArray();
        var snapshot = new BudgetSnapshot(reference.Id, now, usages, ReadActiveHolds(connection, transaction, reference.Id));
        var revision = ReadRevision(connection, transaction);
        _ = checked(revision + expired.Length);
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var row in expired)
        {
            PersistExpiration(connection, transaction, row);
        }
        if (expired.Length > 0)
        {
            SetRevision(connection, transaction, revision + expired.Length);
        }
        return snapshot;
    }
    /// <inheritdoc/>
    public ValueTask<BudgetUnresolvedReservationPage> ReadUnresolvedStartedAsync(BudgetUnresolvedReservationQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var result = _observation.Run("read_unresolved", () => _database.Write((connection, transaction) => ReadUnresolvedCore(connection, transaction, query, cancellationToken), cancellationToken), static _ => "succeeded", query.Scope.Address, query.Scope.Id);
        return ValueTask.FromResult(result);
    }

    private BudgetUnresolvedReservationPage ReadUnresolvedCore(SqliteConnection connection, SqliteTransaction transaction, BudgetUnresolvedReservationQuery query, CancellationToken cancellationToken)
    {
        Debug.Assert(connection is not null && transaction is not null && query is not null, "Validated unresolved-read inputs are required.");
        var scope = RequireScope(connection, transaction, query.Scope);
        if (query.PageSize > scope.Request.Admission.MaximumOpenReservationsPerScope)
        {
            throw new BudgetLedgerStateException("The page size exceeds the captured finite ledger bound.");
        }
        var revision = ReadRevision(connection, transaction);
        var watermark = query.After?.Watermark ?? new BudgetLedgerWatermark(Math.Max(1, revision));
        if (watermark.Value > revision)
        {
            throw new BudgetLedgerStateException("The scan watermark is not known to this ledger state.");
        }
        if (query.After is { } cursor)
        {
            SqliteReservationRow cursorRow;
            try
            {
                cursorRow = RequireReservation(connection, transaction, new BudgetLedgerReservationReference(query.Scope, cursor.AfterReservationId));
            }
            catch (BudgetLedgerReferenceUnavailableException)
            {
                throw new BudgetLedgerStateException("The scan cursor is not valid at its anchored watermark.");
            }
            if (cursorRow.StartedAt is null || cursorRow.StartRevision > watermark.Value)
            {
                throw new BudgetLedgerStateException("The scan cursor is not valid at its anchored watermark.");
            }
        }
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT receipt,receipt_digest,aggregation,started_ticks,started_offset_ticks,start_revision,released,start_expiration,start_expiration_digest,original_commit,original_commit_digest,current_commit,current_commit_digest,accounting_revision,latest_correction_revision FROM budget_reservations WHERE scope_id=$scope AND started_ticks IS NOT NULL AND current_commit IS NULL AND released=0 AND start_revision<=$watermark;";
        _ = command.Parameters.AddWithValue("$scope", query.Scope.Id.Value.ToByteArray());
        _ = command.Parameters.AddWithValue("$watermark", watermark.Value);
        using var reader = command.ExecuteReader();
        var rows = new List<SqliteReservationRow>();
        while (reader.Read())
        {
            var row = ReadReservationRow(reader);
            if (query.After is null || string.CompareOrdinal(row.Receipt.Reservation.Id.Value.ToString("D"), query.After.AfterReservationId.Value.ToString("D")) > 0)
            {
                rows.Add(row);
            }
        }
        rows.Sort(static (left, right) => string.CompareOrdinal(left.Receipt.Reservation.Id.Value.ToString("D"), right.Receipt.Reservation.Id.Value.ToString("D")));
        cancellationToken.ThrowIfCancellationRequested();
        var selected = rows.Take(query.PageSize).Select(row => new BudgetUnresolvedReservation(row.Receipt, row.StartedAt!.Value)).ToImmutableArray();
        var next = rows.Count > selected.Length && selected.Length > 0
            ? new BudgetReservationCursor(query.Scope, watermark, selected[^1].Receipt.Reservation.Id)
            : null;
        return new(query.Scope, watermark, selected, next);
    }
    /// <inheritdoc/>
    public ValueTask<BudgetLedgerReconciliationResult> ReconcileAsync(BudgetLedgerReconciliationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = _observation.Run("reconcile", () => _database.Write((connection, transaction) => ReconcileCore(connection, transaction, request, cancellationToken), cancellationToken), ReconcileOutcome, request.Reservation.Scope.Address, request.Reservation.Scope.Id, request.Reservation.Id);
        return ValueTask.FromResult(result);
    }

    private BudgetLedgerReconciliationResult ReconcileCore(SqliteConnection connection, SqliteTransaction transaction, BudgetLedgerReconciliationRequest request, CancellationToken cancellationToken)
    {
        Debug.Assert(connection is not null && transaction is not null && request is not null, "Validated reconciliation inputs are required.");
        using (var replay = connection.CreateCommand())
        {
            replay.Transaction = transaction;
            replay.CommandText = "SELECT c.evidence,c.evidence_digest,c.result,c.result_digest,c.reservation_id,r.receipt,r.receipt_digest FROM budget_reconciliations c JOIN budget_reservations r ON r.reservation_id=c.reservation_id WHERE c.reservation_id=$reservation AND c.idempotency_key=$key;";
            _ = replay.Parameters.AddWithValue("$key", request.IdempotencyKey.Value);
            _ = replay.Parameters.AddWithValue("$reservation", request.Reservation.Id.Value.ToByteArray());
            using var reader = replay.ExecuteReader();
            if (reader.Read())
            {
                var evidence = DecodeVerified<BudgetReconciliationEvidence>(reader, 0, 1, _settings.MaximumPayloadBytes);
                var replayResult = DecodeVerified<BudgetLedgerReconciliationResult>(reader, 2, 3, _settings.MaximumResultBytes);
                var reservationId = ReadStored(() => new BudgetReservationId(new Guid(ReadBoundedBlob(reader, 4, 16))));
                var receipt = DecodeVerified<BudgetLedgerReservationReceipt>(reader, 5, 6, _settings.MaximumResultBytes);
                return receipt.Reservation != request.Reservation
                    ? throw new BudgetLedgerReferenceUnavailableException("The reservation is unavailable.")
                    : evidence != request.Evidence || reservationId != request.Reservation.Id
                        ? throw new BudgetLedgerMutationConflictException("The reconciliation key is bound to different evidence.")
                        : replayResult;
            }
        }
        var row = RequireReservation(connection, transaction, request.Reservation);
        if (row.CurrentCommit is not null || row.Released)
        {
            throw new BudgetLedgerStateException("Fresh reconciliation evidence cannot target a terminal reservation.");
        }
        if (row.StartedAt is null)
        {
            throw new BudgetLedgerStateException("Only a started reservation can be reconciled.");
        }
        BudgetLedgerReconciliationResult result = request.Evidence switch
        {
            BudgetActualMeasured measured => new BudgetLedgerReconciliationSettled(SettleCore(connection, transaction, new(request.Reservation, measured.Actual), cancellationToken)),
            BudgetActualEstimated estimated => new BudgetLedgerReconciliationSettled(SettleCore(connection, transaction, new(request.Reservation, estimated.Actual), cancellationToken)),
            BudgetNoUsageProven => ReleaseReconciled(connection, transaction, row),
            BudgetStillUnknown => new BudgetLedgerReconciliationRetainedUnknown(request.Reservation),
            _ => throw new InvalidOperationException("The reconciliation evidence kind is unsupported."),
        };
        var evidencePayload = SqliteBudgetLedgerCodec.Encode(request.Evidence, _settings, _settings.MaximumPayloadBytes);
        var resultPayload = SqliteBudgetLedgerCodec.Encode(result, _settings, _settings.MaximumResultBytes);
        var revision = checked(ReadRevision(connection, transaction) + 1);
        cancellationToken.ThrowIfCancellationRequested();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO budget_reconciliations(idempotency_key,reservation_id,evidence,evidence_digest,result,result_digest) VALUES($key,$reservation,$evidence,$evidence_digest,$result,$result_digest); UPDATE budget_ledger_metadata SET revision=$revision;";
        _ = command.Parameters.AddWithValue("$key", request.IdempotencyKey.Value);
        _ = command.Parameters.AddWithValue("$reservation", request.Reservation.Id.Value.ToByteArray());
        _ = command.Parameters.AddWithValue("$evidence", evidencePayload);
        _ = command.Parameters.AddWithValue("$evidence_digest", SHA256.HashData(evidencePayload));
        _ = command.Parameters.AddWithValue("$result", resultPayload);
        _ = command.Parameters.AddWithValue("$result_digest", SHA256.HashData(resultPayload));
        _ = command.Parameters.AddWithValue("$revision", revision);
        _ = command.ExecuteNonQuery();
        return result;
    }
    /// <inheritdoc/>
    public ValueTask<BudgetOverrunHoldResolutionResult> ResolveOverrunHoldAsync(BudgetOverrunHoldResolutionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = _observation.Run("resolve_overrun_hold", () => _database.Write((connection, transaction) => ResolveOverrunHoldCore(connection, transaction, request, cancellationToken), cancellationToken), ResolutionOutcome, request.Hold.Boundary.Address, request.Hold.Boundary.Id, request.Hold.Reservation.Id);
        return ValueTask.FromResult(result);
    }

    private BudgetOverrunHoldResolutionResult ResolveOverrunHoldCore(SqliteConnection connection, SqliteTransaction transaction, BudgetOverrunHoldResolutionRequest request, CancellationToken cancellationToken)
    {
        Debug.Assert(connection is not null && transaction is not null && request is not null, "Validated hold-resolution inputs are required.");
        using (var replay = connection.CreateCommand())
        {
            replay.Transaction = transaction;
            replay.CommandText = "SELECT request,request_digest,result,result_digest FROM budget_resolution_keys WHERE idempotency_key=$key;";
            _ = replay.Parameters.AddWithValue("$key", request.IdempotencyKey.Value);
            using var reader = replay.ExecuteReader();
            if (reader.Read())
            {
                var savedRequest = DecodeVerified<BudgetOverrunHoldResolutionRequest>(reader, 0, 1, _settings.MaximumPayloadBytes);
                return savedRequest != request
                    ? throw new BudgetLedgerMutationConflictException("The overrun-resolution key is bound to different evidence.")
                    : DecodeVerified<BudgetOverrunHoldResolutionResult>(reader, 2, 3, _settings.MaximumResultBytes);
            }
        }

        var boundary = RequireScope(connection, transaction, request.Hold.Boundary);
        _ = RequireReservation(connection, transaction, request.Hold.Reservation);
        using var holdCommand = connection.CreateCommand();
        holdCommand.Transaction = transaction;
        holdCommand.CommandText = "SELECT evidence,evidence_digest,automatically_cleared,resolution FROM budget_overrun_holds WHERE boundary_scope_id=$boundary AND reservation_id=$reservation AND accounting_revision=$revision;";
        _ = holdCommand.Parameters.AddWithValue("$boundary", request.Hold.Boundary.Id.Value.ToByteArray());
        _ = holdCommand.Parameters.AddWithValue("$reservation", request.Hold.Reservation.Id.Value.ToByteArray());
        _ = holdCommand.Parameters.AddWithValue("$revision", request.Hold.TriggeringRevision.Value);
        using var holdReader = holdCommand.ExecuteReader();
        if (!holdReader.Read())
        {
            throw new BudgetLedgerStateException("The overrun hold generation is stale or unavailable.");
        }
        var hold = DecodeVerified<BudgetOverrunHold>(holdReader, 0, 1, _settings.MaximumResultBytes);
        if (!holdReader.IsDBNull(3))
        {
            throw new BudgetLedgerStateException("A fresh key cannot resolve an already terminal overrun hold generation.");
        }
        if (holdReader.GetBoolean(2) || hold.Policy != BudgetOverrunHoldPolicy.RequireAuthorizedResolution)
        {
            throw new BudgetLedgerStateException("The overrun hold generation cannot accept operator resolution.");
        }
        holdReader.Close();
        if (!BudgetOverrunSecurityBinding.Matches(request.Hold, request.EnforcementReceipt))
        {
            throw new BudgetLedgerStateException("The enforcement receipt is not structurally bound to the requested hold.");
        }

        var overruns = ReadActiveHolds(connection, transaction, boundary.Reference.Id)
            .Where(item => item.Dimension == hold.Dimension)
            .Select(item =>
            {
                var row = RequireReservation(connection, transaction, item.Reference.Reservation);
                return new BudgetOverrunHold(item.Reference, item.Dimension, item.Unit, item.Reserved,
                    row.CurrentCommit?.Actual ?? item.CurrentActual, item.Policy);
            })
            .Where(item => item.CurrentActual > item.Reserved)
            .ToImmutableArray();
        var failures = CurrentHardFailures(connection, transaction, boundary, hold.Dimension);
        var nextRevision = checked(ReadRevision(connection, transaction) + 1);
        BudgetOverrunHoldResolutionResult result = !overruns.IsEmpty || !failures.IsEmpty
            ? new BudgetOverrunHoldResolutionBlocked(request.Hold, overruns, failures)
            : new BudgetOverrunHoldResolved(request.Hold, new(nextRevision), request.EnforcementReceipt);
        var requestPayload = SqliteBudgetLedgerCodec.Encode(request, _settings, _settings.MaximumPayloadBytes);
        var resultPayload = SqliteBudgetLedgerCodec.Encode(result, _settings, _settings.MaximumResultBytes);
        cancellationToken.ThrowIfCancellationRequested();
        using var persist = connection.CreateCommand();
        persist.Transaction = transaction;
        persist.CommandText = result is BudgetOverrunHoldResolved
            ? "UPDATE budget_overrun_holds SET resolution=$result,resolution_digest=$result_digest WHERE boundary_scope_id=$boundary AND reservation_id=$reservation AND accounting_revision=$hold_revision; INSERT INTO budget_resolution_keys(idempotency_key,request,request_digest,result,result_digest) VALUES($key,$request,$request_digest,$result,$result_digest); UPDATE budget_ledger_metadata SET revision=$revision;"
            : "INSERT INTO budget_resolution_keys(idempotency_key,request,request_digest,result,result_digest) VALUES($key,$request,$request_digest,$result,$result_digest); UPDATE budget_ledger_metadata SET revision=$revision;";
        _ = persist.Parameters.AddWithValue("$boundary", request.Hold.Boundary.Id.Value.ToByteArray());
        _ = persist.Parameters.AddWithValue("$reservation", request.Hold.Reservation.Id.Value.ToByteArray());
        _ = persist.Parameters.AddWithValue("$hold_revision", request.Hold.TriggeringRevision.Value);
        _ = persist.Parameters.AddWithValue("$key", request.IdempotencyKey.Value);
        _ = persist.Parameters.AddWithValue("$request", requestPayload);
        _ = persist.Parameters.AddWithValue("$request_digest", SHA256.HashData(requestPayload));
        _ = persist.Parameters.AddWithValue("$result", resultPayload);
        _ = persist.Parameters.AddWithValue("$result_digest", SHA256.HashData(resultPayload));
        _ = persist.Parameters.AddWithValue("$revision", nextRevision);
        _ = persist.ExecuteNonQuery();
        return result;
    }

    private ImmutableArray<BudgetLimitFailure> CurrentHardFailures(SqliteConnection connection, SqliteTransaction transaction, SqliteScopeRow boundary, BudgetDimension dimension)
    {
        Debug.Assert(connection is not null && transaction is not null && boundary is not null, "Validated hard-limit inputs are required.");
        var hard = boundary.Request.OriginalRequest.Limits.FirstOrDefault(limit => limit.Dimension == dimension && limit.Kind == BudgetLimitKind.Hard);
        if (hard is null)
        {
            return [];
        }
        var projection = ReadProjection(connection, transaction, boundary.Reference.Id, dimension)
            ?? throw new InvalidDataException("A hold references missing dimension accounting.");
        var aggregation = projection.Aggregation;
        var (reserved, committed) = (projection.Reserved, projection.Committed);
        var observed = aggregation switch
        {
            BudgetAggregationKind.Maximum => Max(reserved, committed),
            BudgetAggregationKind.ConcurrentGauge => reserved,
            BudgetAggregationKind.Sum or BudgetAggregationKind.Duration => reserved.Add(committed),
            _ => throw new BudgetLedgerStateException("The hold accounting has unsupported aggregation semantics."),
        };
        return observed.CompareTo(BudgetQuantity.FromDecimal(hard.Value)) <= 0
            ? []
            : [new(boundary.Reference.Id, dimension, BudgetLimitKind.Hard, hard.Value, observed, default, hard.Unit, "Current accounting exceeds the captured hard boundary.")];
    }

    private static BudgetQuantity MaximumRetained(IEnumerable<SqliteReservationRow> rows,
        IReadOnlyCollection<SqliteReservationRow> excluded, BudgetDimension dimension)
    {
        Debug.Assert(rows is not null, "A validated reservation sequence is required.");
        Debug.Assert(rows is not null && excluded is not null, "Captured reservation collections are required.");
        return rows.Where(row => row.IsCapacityRetaining && row.Receipt.OriginalRequest.Dimension == dimension
                && !excluded.Contains(row))
            .Select(row => BudgetQuantity.FromDecimal(row.Receipt.OriginalRequest.Amount))
            .DefaultIfEmpty(default)
            .Max();
    }

    private static string ScopeOutcome(BudgetLedgerScopeCreateResult result) => result is BudgetLedgerScopeCreated ? "created" : "rejected";
    private static string ReserveOutcome(BudgetLedgerBatchReserveResult result) => result switch
    {
        BudgetLedgerBatchReserved => "reserved",
        BudgetLedgerBatchReserveHeld => "held",
        BudgetLedgerBatchReserveRejected => "rejected",
        _ => "succeeded",
    };
    private static string StartOutcome(BudgetStartResult result) => result switch
    {
        BudgetStartExpired => "expired",
        BudgetStartRejected => "rejected",
        _ => "started",
    };
    private static string ReleaseOutcome(BudgetLedgerReleaseResult result) => result switch
    {
        BudgetLedgerReleased => "released",
        BudgetLedgerRetainedStarted => "retained_started",
        BudgetLedgerAlreadySettled => "already_settled",
        _ => "succeeded",
    };
    private static string ReconcileOutcome(BudgetLedgerReconciliationResult result) => result switch
    {
        BudgetLedgerReconciliationRetainedUnknown => "retained_unknown",
        BudgetLedgerReconciliationReleased => "released",
        BudgetLedgerReconciliationSettled => "settled",
        _ => "succeeded",
    };
    private static string ResolutionOutcome(BudgetOverrunHoldResolutionResult result) =>
        result is BudgetOverrunHoldResolutionBlocked ? "held" : "resolved";
}
