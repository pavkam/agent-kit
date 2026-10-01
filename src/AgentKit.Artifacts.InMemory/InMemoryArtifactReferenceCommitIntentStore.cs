// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.InMemory;

/// <summary>Holds caller-owned reference-commit intents in tenant-partitioned process memory.</summary>
/// <remarks>
/// <para>
/// State is explicitly ephemeral and is lost when the process ends. Every operation is tenant-qualified, so another tenant's intent
/// for the same preparation identity is indistinguishable from an absent one. A late reference commit and collection race through
/// one conditional transition under a lock, so exactly one wins. The instance is thread-safe.
/// </para>
/// <para>Only <see cref="ArtifactReferenceCommitState.Pending"/> to <see cref="ArtifactReferenceCommitState.Committed"/>, <see cref="ArtifactReferenceCommitState.Pending"/> to <see cref="ArtifactReferenceCommitState.Fenced"/>, and <see cref="ArtifactReferenceCommitState.Fenced"/> to <see cref="ArtifactReferenceCommitState.Collected"/> are legal.</para>
/// </remarks>
public sealed class InMemoryArtifactReferenceCommitIntentStore: IArtifactReferenceCommitIntentStore
{
    private readonly Lock _lock = new();
    private readonly ArtifactReferenceCommitIntentTable _table = new();

    /// <inheritdoc/>
    public ValueTask<ArtifactReferenceCommitIntentResult> RecordAsync(ArtifactReferenceCommitIntent intent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(intent);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_lock)
        {
            var decision = _table.PlanRecord(intent);
            _table.Apply(decision);
            return ValueTask.FromResult(decision.Result);
        }
    }

    /// <inheritdoc/>
    public ValueTask<ArtifactReferenceCommitIntentResult> GetAsync(TenantId tenantId, ArtifactPreparationId preparationId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId.Value, nameof(tenantId));
        ArgumentOutOfRangeException.ThrowIfEqual(preparationId, default);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_lock)
        {
            return ValueTask.FromResult(_table.Get(tenantId, preparationId));
        }
    }

    /// <inheritdoc/>
    public ValueTask<ArtifactReferenceCommitIntentResult> TransitionAsync(
        TenantId tenantId,
        ArtifactPreparationId preparationId,
        ArtifactReferenceCommitState expected,
        ArtifactReferenceCommitState nextState,
        DateTimeOffset at,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId.Value, nameof(tenantId));
        ArgumentOutOfRangeException.ThrowIfEqual(preparationId, default);
        ArgumentOutOfRangeException.ThrowIfUndefined(expected);
        ArgumentOutOfRangeException.ThrowIfUndefined(nextState);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_lock)
        {
            var decision = _table.PlanTransition(tenantId, preparationId, expected, nextState, at);
            _table.Apply(decision);
            return ValueTask.FromResult(decision.Result);
        }
    }

    /// <inheritdoc/>
    public ValueTask<ImmutableArray<ArtifactReferenceCommitIntent>> ListPendingAsync(
        TenantId tenantId,
        DateTimeOffset recordedBefore,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId.Value, nameof(tenantId));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_lock)
        {
            return ValueTask.FromResult(_table.ListPending(tenantId, recordedBefore, limit));
        }
    }
}
