// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.InMemory;

using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Stores goals, attempts, and transitions in tenant-partitioned process memory.</summary>
/// <remarks>
/// <para>
/// State lives only as long as this instance: nothing survives the process, so this adapter is for tests, local
/// development, and compositions that explicitly accept ephemeral goals. It claims no durability and its
/// <see cref="GoalStoreDescriptor.SupportsIntentDiscovery"/> reflects only what one process can see.
/// </para>
/// <para>
/// Every operation consumes a single-use grant that binds that exact operation before state is touched, and refuses a
/// request whose authorized agent or session does not own the goal. One gate serializes mutations, which makes the
/// optimistic version check and idempotent replay atomic. The instance is thread-safe.
/// </para>
/// </remarks>
public sealed class InMemoryGoalStore: IGoalStore
{
    private const string _adapter = "in_memory";

    private readonly Lock _gate = new();
    private readonly GoalStoreState _state = new();
    private readonly GoalStoreEnforcement _enforcement;
    private readonly HashSet<ComponentId> _scanners;
    private readonly TimeProvider _time;
    private readonly ILogger<InMemoryGoalStore> _logger;

    /// <summary>Initializes an empty store.</summary>
    /// <param name="grants">The authoritative grant store that validates and consumes each exact operation grant.</param>
    /// <param name="intentIds">The allocator of fresh enforcement-intent identities.</param>
    /// <param name="time">The clock used only for observational duration.</param>
    /// <param name="options">The store options.</param>
    /// <param name="logger">The optional content-free logger.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    public InMemoryGoalStore(
        ISecurityGrantStore grants,
        IIdentifierGenerator<SecurityEnforcementIntentId> intentIds,
        TimeProvider time,
        InMemoryGoalStoreOptions options,
        ILogger<InMemoryGoalStore>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(grants);
        ArgumentNullException.ThrowIfNull(intentIds);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(options);
        Descriptor = new GoalStoreDescriptor("agentkit.goals.in-memory", new ComponentId("agentkit.goals.in-memory"), isDurable: false, supportsIntentDiscovery: true);
        _enforcement = new GoalStoreEnforcement(grants, intentIds, Descriptor.SecurityAudience);
        _scanners = [.. options.AuthorizedIntentScanners];
        _time = time;
        _logger = logger ?? NullLogger<InMemoryGoalStore>.Instance;
    }

    /// <inheritdoc/>
    public GoalStoreDescriptor Descriptor { get; }

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

                lock (_gate)
                {
                    var plan = _state.PlanCreate(request.Grant.Identity.TenantId, request);
                    if (plan.Kind == GoalReductionKind.Rejected)
                    {
                        return new GoalCreateRejected(plan.Failure!);
                    }

                    if (plan.Kind == GoalReductionKind.Applied)
                    {
                        _state.Commit(plan);
                    }

                    return new GoalCreated(plan.Record!, replayed: plan.Kind == GoalReductionKind.Replayed);
                }
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

                GoalRecord? record;
                lock (_gate)
                {
                    record = _state.Find(request.Grant.Identity.TenantId, request.GoalId);
                }

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

                lock (_gate)
                {
                    var plan = _state.PlanTransition(request.Grant.Identity.TenantId, request);
                    if (plan.Kind == GoalReductionKind.Rejected)
                    {
                        return new GoalTransitionRejected(plan.Failure!);
                    }

                    if (plan.Kind == GoalReductionKind.Applied)
                    {
                        _state.Commit(plan);
                    }

                    return new GoalTransitioned(plan.Record!, replayed: plan.Kind == GoalReductionKind.Replayed);
                }
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

                lock (_gate)
                {
                    return _state.ReadChildren(request.Grant.Identity.TenantId, request);
                }
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
                if (!_scanners.Contains(request.Scanner))
                {
                    return ValueTask.FromResult<GoalPageResult>(new GoalPageRejected(
                        new GoalStoreFailure(GoalStoreFailureKind.Denied, "The scanner is not configured for this store.")));
                }

                lock (_gate)
                {
                    return ValueTask.FromResult(_state.ReadIntents(request));
                }
            },
            static result => (result as GoalPageRejected)?.Failure);
    }
}
