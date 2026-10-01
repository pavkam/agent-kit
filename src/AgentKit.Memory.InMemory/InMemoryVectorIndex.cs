// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.InMemory;

using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Searches vectors of exactly one space with a brute-force scan over tenant-partitioned process memory.</summary>
/// <remarks>
/// <para>
/// Search scores every visible stored vector under the space's metric, so results are exact and deterministic and the index
/// never claims approximate search. State lives only as long as this instance: nothing survives the process.
/// </para>
/// <para>
/// Every request names its complete vector space, and a mismatch is refused before any grant is consumed or state is read.
/// Every operation then consumes a single-use grant that binds that exact operation. One gate serializes access. The instance
/// is thread-safe.
/// </para>
/// </remarks>
public sealed class InMemoryVectorIndex: IVectorIndex
{
    private const string _adapter = "in_memory";

    private readonly Lock _gate = new();
    private readonly VectorStoreState _state = new();
    private readonly MemoryStoreEnforcement _enforcement;
    private readonly TimeProvider _time;
    private readonly ILogger<InMemoryVectorIndex> _logger;

    /// <summary>Initializes an empty index.</summary>
    /// <param name="space">The single vector space the index holds.</param>
    /// <param name="grants">The authoritative grant store that validates and consumes each exact operation grant.</param>
    /// <param name="intentIds">The allocator of fresh enforcement-intent identities.</param>
    /// <param name="time">The clock used only for observational duration.</param>
    /// <param name="logger">The optional content-free logger.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    public InMemoryVectorIndex(
        VectorSpaceDescriptor space,
        ISecurityGrantStore grants,
        IIdentifierGenerator<SecurityEnforcementIntentId> intentIds,
        TimeProvider time,
        ILogger<InMemoryVectorIndex>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(space);
        ArgumentNullException.ThrowIfNull(grants);
        ArgumentNullException.ThrowIfNull(intentIds);
        ArgumentNullException.ThrowIfNull(time);
        VectorSpace = space;
        SecurityAudience = new ComponentId("agentkit.vectors.in-memory");
        _enforcement = new MemoryStoreEnforcement(grants, intentIds, SecurityAudience);
        _time = time;
        _logger = logger ?? NullLogger<InMemoryVectorIndex>.Instance;
    }

    /// <inheritdoc/>
    public VectorSpaceDescriptor VectorSpace { get; }

    /// <inheritdoc/>
    public ComponentId SecurityAudience { get; }

    /// <inheritdoc/>
    public bool IsDurable => false;

    /// <inheritdoc/>
    public bool ApproximateSearch => false;

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

            if (await _enforcement.ConsumeAsync(
                request.Grant, SecurityOperationKind.StateMutation, SecurityEffect.CreateOrReplace,
                [VectorSecurityBinding.Resource(VectorSpace.IndexKey)],
                VectorSecurityBinding.UpsertFingerprint(request.Space, request.Records, request.IdempotencyKey), cancellationToken).ConfigureAwait(false) is { } denial)
            {
                return VectorUpsertResult.Rejected(denial);
            }

            lock (_gate)
            {
                var plan = VectorPlanner.PlanUpsert(_state, request);
                _state.Commit(plan.Upserts, plan.Deletes, plan.Receipt, plan.Watermark);
                return plan.Result;
            }
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

            if (await _enforcement.ConsumeAsync(
                request.Grant, SecurityOperationKind.StateRead, SecurityEffect.Observe,
                [VectorSecurityBinding.Resource(VectorSpace.IndexKey)],
                VectorSecurityBinding.SearchFingerprint(request.Space, request.Query, request.TopK, request.Documents), cancellationToken).ConfigureAwait(false) is { } denial)
            {
                return VectorSearchResult.Rejected(denial);
            }

            lock (_gate)
            {
                return VectorPlanner.Search(_state, VectorSpace, request);
            }
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

            if (await _enforcement.ConsumeAsync(
                request.Grant, SecurityOperationKind.StateMutation, SecurityEffect.Delete,
                [VectorSecurityBinding.Resource(VectorSpace.IndexKey)],
                VectorSecurityBinding.DeleteFingerprint(request.Space, request.ChunkIds, request.IdempotencyKey), cancellationToken).ConfigureAwait(false) is { } denial)
            {
                return VectorDeleteResult.Rejected(denial);
            }

            lock (_gate)
            {
                var plan = VectorPlanner.PlanDelete(_state, request);
                _state.Commit(plan.Upserts, plan.Deletes, plan.Receipt, plan.Watermark);
                return plan.Result;
            }
        }, static result => result.Failure);
    }

    private ValueTask<TResult> Observe<TResult>(
        MemoryStoreOperationKind kind,
        SecurityGrant grant,
        Func<ValueTask<TResult>> operation,
        Func<TResult, MemoryStoreFailure?> failureOf) =>
        MemoryStoreObservation.ObserveAsync(_logger, _time, _adapter, MemoryStoreFamily.Vector, kind, grant.Identity.TenantId, operation, failureOf);
}
