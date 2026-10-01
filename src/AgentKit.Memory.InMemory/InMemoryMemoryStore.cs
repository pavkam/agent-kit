// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.InMemory;

using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Stores durable memory records in tenant-partitioned process memory.</summary>
/// <remarks>
/// <para>
/// State lives only as long as this instance: nothing survives the process, so this adapter is for tests, local development,
/// and compositions that explicitly accept ephemeral memory. It claims no durability.
/// </para>
/// <para>
/// Every operation consumes a single-use grant that binds that exact operation before state is touched, and resolves identities
/// only inside the authorized tenant, agent, and principal visibility. One gate serializes mutations, which makes the version
/// check, idempotent replay, and deletion-generation assignment atomic. The instance is thread-safe.
/// </para>
/// </remarks>
public sealed class InMemoryMemoryStore: IMemoryStore
{
    private const string _adapter = "in_memory";

    private readonly Lock _gate = new();
    private readonly MemoryStoreState _state = new();
    private readonly MemoryStoreEnforcement _enforcement;
    private readonly TimeProvider _time;
    private readonly ILogger<InMemoryMemoryStore> _logger;

    /// <summary>Initializes an empty store.</summary>
    /// <param name="key">The key the store is registered and selected under.</param>
    /// <param name="grants">The authoritative grant store that validates and consumes each exact operation grant.</param>
    /// <param name="intentIds">The allocator of fresh enforcement-intent identities.</param>
    /// <param name="time">The clock used only for observational duration.</param>
    /// <param name="logger">The optional content-free logger.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> is default or blank.</exception>
    public InMemoryMemoryStore(
        MemoryStoreKey key,
        ISecurityGrantStore grants,
        IIdentifierGenerator<SecurityEnforcementIntentId> intentIds,
        TimeProvider time,
        ILogger<InMemoryMemoryStore>? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        ArgumentNullException.ThrowIfNull(grants);
        ArgumentNullException.ThrowIfNull(intentIds);
        ArgumentNullException.ThrowIfNull(time);
        Descriptor = new MemoryStoreDescriptor(key, "agentkit.memory.in-memory", new ComponentId("agentkit.memory.in-memory"), isDurable: false);
        _enforcement = new MemoryStoreEnforcement(grants, intentIds, Descriptor.SecurityAudience);
        _time = time;
        _logger = logger ?? NullLogger<InMemoryMemoryStore>.Instance;
    }

    /// <inheritdoc/>
    public MemoryStoreDescriptor Descriptor { get; }

    /// <inheritdoc/>
    public ValueTask<MemoryWriteResult> WriteAsync(MemoryWriteRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Observe(MemoryStoreOperationKind.Write, request.Grant, async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await _enforcement.ConsumeAsync(
                request.Grant, SecurityOperationKind.StateMutation, SecurityEffect.Create,
                [MemorySecurityBinding.Resource(request.Record.Id)], MemorySecurityBinding.WriteFingerprint(request.Record, request.IdempotencyKey), cancellationToken).ConfigureAwait(false) is { } denial)
            {
                return MemoryWriteResult.Rejected(denial);
            }

            lock (_gate)
            {
                var plan = MemoryPlanner.PlanWrite(_state, request);
                _state.Commit(plan.Upserts);
                return plan.Result;
            }
        }, static result => result.Failure);
    }

    /// <inheritdoc/>
    public ValueTask<MemoryReadResult> ReadAsync(MemoryReadRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Observe(MemoryStoreOperationKind.Read, request.Grant, async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await _enforcement.ConsumeAsync(
                request.Grant, SecurityOperationKind.StateRead, SecurityEffect.Observe,
                [MemorySecurityBinding.Resource(request.Id)], MemorySecurityBinding.ReadFingerprint(request.Id), cancellationToken).ConfigureAwait(false) is { } denial)
            {
                return MemoryReadResult.Rejected(denial);
            }

            lock (_gate)
            {
                return MemoryPlanner.Read(_state, request);
            }
        }, static result => result.Failure);
    }

    /// <inheritdoc/>
    public ValueTask<MemoryListResult> ListAsync(MemoryListRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Observe(MemoryStoreOperationKind.List, request.Grant, async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await _enforcement.ConsumeAsync(
                request.Grant, SecurityOperationKind.StateRead, SecurityEffect.Observe,
                [MemorySecurityBinding.CollectionResource(request.Grant.Scope.AgentId)],
                MemorySecurityBinding.ListFingerprint(request.Namespace, request.States, request.Terms, request.AfterSequence, request.Limit), cancellationToken).ConfigureAwait(false) is { } denial)
            {
                return MemoryListResult.Rejected(denial);
            }

            lock (_gate)
            {
                return MemoryPlanner.List(_state, request);
            }
        }, static result => result.Failure);
    }

    /// <inheritdoc/>
    public ValueTask<MemoryTransitionResult> TransitionAsync(MemoryTransitionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Observe(MemoryStoreOperationKind.Transition, request.Grant, async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await _enforcement.ConsumeAsync(
                request.Grant, SecurityOperationKind.StateMutation, SecurityEffect.Mutate,
                [MemorySecurityBinding.Resource(request.Id)],
                MemorySecurityBinding.TransitionFingerprint(request.Id, request.To, request.ExpectedVersion, request.Replacement, request.IdempotencyKey, request.At), cancellationToken).ConfigureAwait(false) is { } denial)
            {
                return MemoryTransitionResult.Rejected(denial);
            }

            lock (_gate)
            {
                var plan = MemoryPlanner.PlanTransition(_state, request);
                _state.Commit(plan.Upserts);
                return plan.Result;
            }
        }, static result => result.Failure);
    }

    /// <inheritdoc/>
    public ValueTask<MemoryDeleteResult> DeleteAsync(MemoryDeleteRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Observe(MemoryStoreOperationKind.Delete, request.Grant, async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await _enforcement.ConsumeAsync(
                request.Grant, SecurityOperationKind.StateMutation, SecurityEffect.Delete,
                [MemorySecurityBinding.Resource(request.Id)],
                MemorySecurityBinding.DeleteFingerprint(request.Id, request.ExpectedVersion, request.Mode, request.IdempotencyKey, request.At), cancellationToken).ConfigureAwait(false) is { } denial)
            {
                return MemoryDeleteResult.Rejected(denial);
            }

            lock (_gate)
            {
                var plan = MemoryPlanner.PlanDelete(_state, request, Descriptor.Name);
                _state.Commit(plan.Upserts);
                return plan.Result;
            }
        }, static result => result.Failure);
    }

    private ValueTask<TResult> Observe<TResult>(
        MemoryStoreOperationKind kind,
        SecurityGrant grant,
        Func<ValueTask<TResult>> operation,
        Func<TResult, MemoryStoreFailure?> failureOf) =>
        MemoryStoreObservation.ObserveAsync(_logger, _time, _adapter, MemoryStoreFamily.Memory, kind, grant.Identity.TenantId, operation, failureOf);
}
