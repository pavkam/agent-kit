// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Sqlite;

using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Stores goals, attempts, and transitions in one host-local SQLite database.</summary>
/// <remarks>
/// <para>
/// Each goal is one row holding its aggregate, with indexed columns for tenant, parent, creation sequence, child ordinal,
/// status, and settlement sequence. Every mutation runs in an immediate transaction that reads stored state through the
/// shared planner and writes the result, so the version check, idempotent replay, sequence allocation, and child ordinal
/// are atomic even across processes sharing the file. An acknowledged write is committed and survives process loss.
/// </para>
/// <para>
/// The captured authorization of a delegated child is persisted, so <see cref="ReadIntentsAsync"/> lets a restarted worker
/// rediscover open children. Every operation consumes a single-use grant that binds that exact operation before the
/// transaction opens. SQLite provides durable local storage only: it implies no distributed lease, fencing, or
/// cross-store atomicity. The instance is thread-safe.
/// </para>
/// </remarks>
public sealed class SqliteGoalStore: IGoalStore
{
    private const string _adapter = "sqlite";

    private static readonly JsonSerializerOptions _json = JsonStoreSerialization.CreateCanonicalOptions();

    private readonly SqliteGoalDatabase _database;
    private readonly GoalStoreEnforcement _enforcement;
    private readonly HashSet<ComponentId> _scanners;
    private readonly TimeProvider _time;
    private readonly ILogger<SqliteGoalStore> _logger;

    /// <summary>Initializes a store bound to one host-authorized database without opening it.</summary>
    /// <param name="target">The non-null exact database and bootstrap effects.</param>
    /// <param name="settings">The non-null immutable lock and size bounds.</param>
    /// <param name="grants">The authoritative grant store that validates and consumes each exact grant.</param>
    /// <param name="intentIds">The allocator of fresh enforcement-intent identities.</param>
    /// <param name="time">The clock used only for observational duration.</param>
    /// <param name="logger">The optional content-free logger.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    public SqliteGoalStore(
        SqliteGoalStoreTarget target,
        SqliteGoalStoreSettings settings,
        ISecurityGrantStore grants,
        IIdentifierGenerator<SecurityEnforcementIntentId> intentIds,
        TimeProvider time,
        ILogger<SqliteGoalStore>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(grants);
        ArgumentNullException.ThrowIfNull(intentIds);
        ArgumentNullException.ThrowIfNull(time);
        Descriptor = new GoalStoreDescriptor("agentkit.goals.sqlite", new ComponentId("agentkit.goals.sqlite"), isDurable: true, supportsIntentDiscovery: true);
        _database = new SqliteGoalDatabase(target, settings);
        _enforcement = new GoalStoreEnforcement(grants, intentIds, Descriptor.SecurityAudience);
        _scanners = [.. settings.AuthorizedIntentScanners];
        _time = time;
        _logger = logger ?? NullLogger<SqliteGoalStore>.Instance;
    }

    /// <inheritdoc/>
    public GoalStoreDescriptor Descriptor { get; }

    /// <summary>Creates or validates the schema and binds the database to its expected identity.</summary>
    /// <param name="cancellationToken">Cancels before initialization completes.</param>
    /// <returns>A task completed once the database is ready for operations.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    /// <exception cref="InvalidOperationException">The database is missing, uninitialized, belongs to another instance, or has an unsupported schema.</exception>
    /// <remarks>Calling this is optional: the first operation initializes the store. Calling it during trusted host startup surfaces a misconfigured database at boot. Repeating it after success is a no-op.</remarks>
    public ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        _database.EnsureInitialized(cancellationToken);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<GoalCreateResult> CreateAsync(GoalCreateRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return GoalStoreObservation.ObserveAsync<GoalCreateResult>(
            _logger, _time, _adapter, GoalStoreOperationKind.Create, request.Goal.Id, request.Grant.Identity.TenantId,
            async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (await _enforcement.ConsumeAsync(
                    request.Grant, SecurityOperationKind.StateMutation, SecurityEffect.Create,
                    [GoalSecurityBinding.Resource(request.Goal.Id)], GoalSecurityBinding.Fingerprint(request), cancellationToken).ConfigureAwait(false) is { } denial)
                {
                    return new GoalCreateRejected(denial);
                }

                if (GoalStoreEnforcement.CheckOwner(request.Grant, request.Goal.OwnerAgentId, request.Goal.SessionId) is { } mismatch)
                {
                    return new GoalCreateRejected(mismatch);
                }

                var tenant = request.Grant.Identity.TenantId;
                var plan = InTransaction(
                    "create",
                    (connection, transaction, lookup) =>
                    {
                        var planned = GoalPlanner.PlanCreate(lookup, tenant, request);
                        if (planned.Kind == GoalReductionKind.Applied)
                        {
                            Insert(connection, transaction, planned);
                        }

                        return planned;
                    },
                    cancellationToken);
                return plan.Kind == GoalReductionKind.Rejected
                    ? new GoalCreateRejected(plan.Failure!)
                    : new GoalCreated(plan.Record!, replayed: plan.Kind == GoalReductionKind.Replayed);
            },
            static result => (result as GoalCreateRejected)?.Failure);
    }

    /// <inheritdoc/>
    public ValueTask<GoalLoadResult> LoadAsync(GoalLoadRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return GoalStoreObservation.ObserveAsync<GoalLoadResult>(
            _logger, _time, _adapter, GoalStoreOperationKind.Load, request.GoalId, request.Grant.Identity.TenantId,
            async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (await _enforcement.ConsumeAsync(
                    request.Grant, SecurityOperationKind.StateRead, SecurityEffect.Observe,
                    [GoalSecurityBinding.Resource(request.GoalId)], GoalSecurityBinding.Fingerprint(request), cancellationToken).ConfigureAwait(false) is { } denial)
                {
                    return new GoalLoadRejected(denial);
                }

                var record = InTransaction("load", (_, _, lookup) => lookup.Find(request.Grant.Identity.TenantId, request.GoalId), cancellationToken);
                return record is null
                    ? new GoalLoadRejected(new GoalStoreFailure(GoalStoreFailureKind.NotFound, "The goal does not exist."))
                    : GoalStoreEnforcement.CheckOwner(request.Grant, record.Goal.OwnerAgentId, record.Goal.SessionId) is { } mismatch
                        ? new GoalLoadRejected(mismatch)
                        : new GoalLoaded(record);
            },
            static result => (result as GoalLoadRejected)?.Failure);
    }

    /// <inheritdoc/>
    public ValueTask<GoalTransitionResult> TransitionAsync(GoalTransitionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return GoalStoreObservation.ObserveAsync<GoalTransitionResult>(
            _logger, _time, _adapter, GoalStoreOperationKind.Transition, request.Transition.GoalId, request.Grant.Identity.TenantId,
            async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (await _enforcement.ConsumeAsync(
                    request.Grant, SecurityOperationKind.StateMutation, SecurityEffect.Mutate,
                    [GoalSecurityBinding.Resource(request.Transition.GoalId)], GoalSecurityBinding.Fingerprint(request), cancellationToken).ConfigureAwait(false) is { } denial)
                {
                    return new GoalTransitionRejected(denial);
                }

                if (GoalStoreEnforcement.CheckOwner(request.Grant, request.Transition.OwnerAgentId, request.Transition.SessionId) is { } mismatch)
                {
                    return new GoalTransitionRejected(mismatch);
                }

                var tenant = request.Grant.Identity.TenantId;
                var plan = InTransaction(
                    "transition",
                    (connection, transaction, lookup) =>
                    {
                        var planned = GoalPlanner.PlanTransition(lookup, tenant, request);
                        if (planned.Kind == GoalReductionKind.Applied)
                        {
                            Update(connection, transaction, planned);
                        }

                        return planned;
                    },
                    cancellationToken);
                return plan.Kind == GoalReductionKind.Rejected
                    ? new GoalTransitionRejected(plan.Failure!)
                    : new GoalTransitioned(plan.Record!, replayed: plan.Kind == GoalReductionKind.Replayed);
            },
            static result => (result as GoalTransitionRejected)?.Failure);
    }

    /// <inheritdoc/>
    public ValueTask<GoalPageResult> ReadChildrenAsync(GoalChildrenRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return GoalStoreObservation.ObserveAsync(
            _logger, _time, _adapter, GoalStoreOperationKind.ReadChildren, request.ParentId, request.Grant.Identity.TenantId,
            async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (await _enforcement.ConsumeAsync(
                    request.Grant, SecurityOperationKind.StateRead, SecurityEffect.Observe,
                    [GoalSecurityBinding.ChildrenResource(request.ParentId)], GoalSecurityBinding.Fingerprint(request), cancellationToken).ConfigureAwait(false) is { } denial)
                {
                    return new GoalPageRejected(denial);
                }

                var tenant = request.Grant.Identity.TenantId;
                return InTransaction(
                    "read_children",
                    (_, _, lookup) => ReadChildrenPage(lookup, tenant, request),
                    cancellationToken);
            },
            static result => (result as GoalPageRejected)?.Failure);
    }

    /// <inheritdoc/>
    public ValueTask<GoalPageResult> ReadIntentsAsync(GoalIntentScanRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return GoalStoreObservation.ObserveAsync(
            _logger, _time, _adapter, GoalStoreOperationKind.ReadIntents, null, null,
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return ValueTask.FromResult(
                    !_scanners.Contains(request.Scanner)
                        ? new GoalPageRejected(new GoalStoreFailure(GoalStoreFailureKind.Denied, "The scanner is not configured for this store."))
                        : InTransaction<GoalPageResult>("read_intents", (_, _, lookup) => ReadIntentsPage(lookup, request), cancellationToken));
            },
            static result => (result as GoalPageRejected)?.Failure);
    }

    private static GoalPage ReadIntentsPage(SqliteGoalLookup lookup, GoalIntentScanRequest request)
    {
        var page = lookup.ReadIntents(request);
        var more = page.Count > request.Limit;
        var items = page.Take(request.Limit).ToImmutableArray();
        return new GoalPage(items, more ? items[^1].Sequence : null);
    }

    private static GoalPageResult ReadChildrenPage(SqliteGoalLookup lookup, TenantId tenant, GoalChildrenRequest request)
    {
        var parent = lookup.Find(tenant, request.ParentId);
        var failure = parent is null
            ? new GoalStoreFailure(GoalStoreFailureKind.NotFound, "The goal does not exist.")
            : GoalStoreEnforcement.CheckOwner(request.Grant, parent.Goal.OwnerAgentId, parent.Goal.SessionId);
        if (failure is not null)
        {
            return new GoalPageRejected(failure);
        }

        var page = lookup.ReadChildren(tenant, request);
        var more = page.Count > request.Limit;
        var items = page.Take(request.Limit).ToImmutableArray();
        return new GoalPage(items, more ? items[^1].ChildOrdinal : null);
    }

    private TResult InTransaction<TResult>(
        string operation,
        Func<SqliteConnection, SqliteTransaction, SqliteGoalLookup, TResult> work,
        CancellationToken cancellationToken)
    {
        _database.EnsureInitialized(cancellationToken);
        using var connection = _database.Open();
        using var transaction = connection.BeginTransaction(deferred: false);
        try
        {
            var result = work(connection, transaction, new SqliteGoalLookup(connection, transaction, _json));
            cancellationToken.ThrowIfCancellationRequested();
            transaction.Commit();
            return result;
        }
        catch (SqliteException exception)
        {
            GoalStoreObservation.Safe(() => SqliteGoalStoreLog.CommitFailed(_logger, operation, exception.SqliteErrorCode));
            throw;
        }
    }

    private void Insert(SqliteConnection connection, SqliteTransaction transaction, GoalPlan plan)
    {
        var record = plan.Record!;
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO goals(tenant, goal_id, parent_id, sequence, child_ordinal, status, delegated, settled_sequence, document) "
            + "VALUES ($tenant, $goal, $parent, $sequence, $ordinal, $status, $delegated, $settled, $document)";
        Bind(command, plan, record);
        _ = command.ExecuteNonQuery();
        using var creation = connection.CreateCommand();
        creation.Transaction = transaction;
        creation.CommandText = "INSERT INTO goal_creations(tenant, creation_key, goal_id) VALUES ($tenant, $key, $goal)";
        _ = creation.Parameters.AddWithValue("$tenant", plan.Tenant.Value);
        _ = creation.Parameters.AddWithValue("$key", plan.CreateKey);
        _ = creation.Parameters.AddWithValue("$goal", SqliteGoalDatabase.Encode(record.Goal.Id.Value));
        _ = creation.ExecuteNonQuery();
    }

    private void Update(SqliteConnection connection, SqliteTransaction transaction, GoalPlan plan)
    {
        var record = plan.Record!;
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "UPDATE goals SET status = $status, settled_sequence = $settled, document = $document WHERE tenant = $tenant AND goal_id = $goal";
        _ = command.Parameters.AddWithValue("$tenant", plan.Tenant.Value);
        _ = command.Parameters.AddWithValue("$goal", SqliteGoalDatabase.Encode(record.Goal.Id.Value));
        _ = command.Parameters.AddWithValue("$status", (int) record.Goal.Status);
        _ = command.Parameters.AddWithValue("$settled", (object?) record.SettledSequence ?? DBNull.Value);
        _ = command.Parameters.AddWithValue("$document", Encode(plan, record));
        if (command.ExecuteNonQuery() != 1)
        {
            throw SqliteGoalDatabase.Unavailable("The goal row changed or vanished during an immediate transaction.");
        }
    }

    private void Bind(SqliteCommand command, GoalPlan plan, GoalRecord record)
    {
        _ = command.Parameters.AddWithValue("$tenant", plan.Tenant.Value);
        _ = command.Parameters.AddWithValue("$goal", SqliteGoalDatabase.Encode(record.Goal.Id.Value));
        _ = command.Parameters.AddWithValue("$parent", record.Goal.ParentId is { } parent ? SqliteGoalDatabase.Encode(parent.Value) : DBNull.Value);
        _ = command.Parameters.AddWithValue("$sequence", record.Sequence);
        _ = command.Parameters.AddWithValue("$ordinal", (object?) record.ChildOrdinal ?? DBNull.Value);
        _ = command.Parameters.AddWithValue("$status", (int) record.Goal.Status);
        _ = command.Parameters.AddWithValue("$delegated", record.Delegation is null ? 0 : 1);
        _ = command.Parameters.AddWithValue("$settled", (object?) record.SettledSequence ?? DBNull.Value);
        _ = command.Parameters.AddWithValue("$document", Encode(plan, record));
    }

    private string Encode(GoalPlan plan, GoalRecord record)
    {
        var bytes = JsonStoreSerialization.Encode(
            PersistedGoalDocument.FromDomain(plan.Tenant, plan.CreateKey, record), _json, _database.Settings.MaximumRecordBytes);
        return System.Text.Encoding.UTF8.GetString(bytes);
    }
}
