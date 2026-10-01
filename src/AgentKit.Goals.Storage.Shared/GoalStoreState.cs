// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Storage;

/// <summary>Holds tenant-partitioned goal aggregates and their indexes for adapters that keep state in process memory.</summary>
/// <remarks>
/// The class is not thread-safe: the owning adapter serializes every call under its own gate. Mutations are planned first
/// and committed second so a durable adapter can write the planned aggregate before memory advances. State is partitioned
/// by tenant, so the same identity in two tenants is two different goals and one tenant can never observe another's.
/// </remarks>
internal sealed class GoalStoreState: IGoalLookup
{
    private readonly Dictionary<(TenantId Tenant, GoalId Goal), GoalRecord> _goals = [];
    private readonly Dictionary<(TenantId Tenant, string Key), GoalId> _creations = [];
    private readonly Dictionary<(TenantId Tenant, GoalId Parent), List<GoalId>> _children = [];
    private long _sequence;
    private long _settled;

    /// <inheritdoc/>
    public long NextSequence => _sequence + 1;

    /// <inheritdoc/>
    public long NextSettledSequence => _settled + 1;

    /// <inheritdoc/>
    public GoalRecord? FindByCreationKey(TenantId tenant, string key) =>
        _creations.TryGetValue((tenant, key), out var id) ? _goals[(tenant, id)] : null;

    /// <inheritdoc/>
    public int CountChildren(TenantId tenant, GoalId parentId) =>
        _children.TryGetValue((tenant, parentId), out var siblings) ? siblings.Count : 0;

    /// <summary>Gets the number of stored goals across every tenant.</summary>
    internal int Count => _goals.Count;

    /// <summary>Lists every stored snapshot with its creation key, in creation order, for compaction.</summary>
    /// <returns>One latest snapshot per goal, ordered by creation sequence.</returns>
    internal IReadOnlyList<(TenantId Tenant, string? CreateKey, GoalRecord Record)> Snapshot()
    {
        var keys = _creations.ToDictionary(static pair => (pair.Key.Tenant, pair.Value), static pair => pair.Key.Key);
        return
        [
            .. _goals
                .OrderBy(static pair => pair.Value.Sequence)
                .Select(pair => (pair.Key.Tenant, keys.GetValueOrDefault((pair.Key.Tenant, pair.Key.Goal)), pair.Value)),
        ];
    }

    /// <inheritdoc/>
    public GoalRecord? Find(TenantId tenant, GoalId goalId) => _goals.GetValueOrDefault((tenant, goalId));

    /// <summary>Plans one goal creation without changing state.</summary>
    /// <param name="tenant">The tenant partition.</param>
    /// <param name="request">The create request, already authorized.</param>
    /// <returns>An applied plan, a replay, or a typed rejection.</returns>
    internal GoalPlan PlanCreate(TenantId tenant, GoalCreateRequest request) => GoalPlanner.PlanCreate(this, tenant, request);

    /// <summary>Plans one transition without changing state.</summary>
    /// <param name="tenant">The tenant partition.</param>
    /// <param name="request">The transition request, already authorized.</param>
    /// <returns>An applied plan, a replay, or a typed rejection.</returns>
    internal GoalPlan PlanTransition(TenantId tenant, GoalTransitionRequest request) => GoalPlanner.PlanTransition(this, tenant, request);

    /// <summary>Commits an applied plan to memory after the adapter persisted it.</summary>
    /// <param name="plan">The applied plan.</param>
    internal void Commit(GoalPlan plan)
    {
        Debug.Assert(plan is { Kind: GoalReductionKind.Applied, Record: not null }, "Only an applied plan carries something to commit.");
        Restore(plan.Tenant, plan.CreateKey, plan.Record);
    }

    /// <summary>Installs one aggregate, used both to commit a plan and to rebuild memory from a durable log.</summary>
    /// <param name="tenant">The tenant partition.</param>
    /// <param name="createKey">The creation idempotency key to index, or <see langword="null"/> for a later snapshot of an existing goal.</param>
    /// <param name="record">The aggregate to install.</param>
    internal void Restore(TenantId tenant, string? createKey, GoalRecord record)
    {
        Debug.Assert(record is not null, "Restoring installs an aggregate.");
        var key = (tenant, record.Goal.Id);
        var isNew = !_goals.ContainsKey(key);
        _goals[key] = record;
        if (isNew)
        {
            if (record.Goal.ParentId is { } parentId)
            {
                if (!_children.TryGetValue((tenant, parentId), out var siblings))
                {
                    siblings = [];
                    _children[(tenant, parentId)] = siblings;
                }

                siblings.Add(record.Goal.Id);
            }
        }

        if (createKey is not null)
        {
            _creations[(tenant, createKey)] = record.Goal.Id;
        }

        _sequence = Math.Max(_sequence, record.Sequence);
        if (record.SettledSequence is { } settled)
        {
            _settled = Math.Max(_settled, settled);
        }
    }

    /// <summary>Reads one page of a goal's children in recorded ordinal order.</summary>
    /// <param name="tenant">The tenant partition.</param>
    /// <param name="request">The children request, already authorized.</param>
    /// <returns>The page or a typed rejection.</returns>
    internal GoalPageResult ReadChildren(TenantId tenant, GoalChildrenRequest request)
    {
        Debug.Assert(request is not null, "Adapters validate the request before reading.");
        var parent = Find(tenant, request.ParentId);
        if (parent is null)
        {
            return new GoalPageRejected(new GoalStoreFailure(GoalStoreFailureKind.NotFound, "The goal does not exist."));
        }

        if (GoalStoreEnforcement.CheckOwner(request.Grant, parent.Goal.OwnerAgentId, parent.Goal.SessionId) is { } failure)
        {
            return new GoalPageRejected(failure);
        }

        var children = _children.TryGetValue((tenant, request.ParentId), out var ids)
            ? ids.Select(id => _goals[(tenant, id)]).Where(record => record.ChildOrdinal > request.AfterOrdinal).Take(request.Limit + 1).ToList()
            : [];
        var more = children.Count > request.Limit;
        var items = children.Take(request.Limit).ToImmutableArray();
        return new GoalPage(items, more ? items[^1].ChildOrdinal : null);
    }

    /// <summary>Reads one page of open delegated children across tenants, in creation order.</summary>
    /// <param name="request">The scan request from a configured scanner.</param>
    /// <returns>The page.</returns>
    internal GoalPageResult ReadIntents(GoalIntentScanRequest request)
    {
        Debug.Assert(request is not null, "Adapters validate the request before reading.");
        var open = _goals.Values
            .Where(record => record.Delegation is not null
                && record.Goal.Status is GoalStatus.Ready or GoalStatus.Active
                && record.Sequence > request.AfterSequence)
            .OrderBy(static record => record.Sequence)
            .Take(request.Limit + 1)
            .ToList();
        var more = open.Count > request.Limit;
        var items = open.Take(request.Limit).ToImmutableArray();
        return new GoalPage(items, more ? items[^1].Sequence : null);
    }
}
