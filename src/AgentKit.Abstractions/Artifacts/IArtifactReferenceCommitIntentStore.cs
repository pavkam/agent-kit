// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Holds caller-owned reference-commit intents so orphaned artifacts stay reconcilable.</summary>
/// <remarks>
/// Every method is tenant-qualified, idempotent, and conditional: a late reference commit and collection race through
/// <see cref="TransitionAsync"/> and exactly one wins. The store belongs to the caller that owns the referencing record; the artifact
/// runtime only fences and completes intents through this contract and never reads session, tool, or memory state.
/// </remarks>
public interface IArtifactReferenceCommitIntentStore
{
    /// <summary>Records an intent before finalization.</summary>
    /// <param name="intent">The intent to record, normally in <see cref="ArtifactReferenceCommitState.Pending"/>.</param>
    /// <param name="cancellationToken">Cancels before the intent is recorded.</param>
    /// <returns>Applied on first recording, replayed for an equal intent, or conflict when another intent holds the tenant preparation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="intent"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public ValueTask<ArtifactReferenceCommitIntentResult> RecordAsync(ArtifactReferenceCommitIntent intent, CancellationToken cancellationToken = default);

    /// <summary>Reads the authoritative intent for a tenant preparation.</summary>
    /// <param name="tenantId">The tenant partition.</param>
    /// <param name="preparationId">The preparation identity.</param>
    /// <param name="cancellationToken">Cancels before the read completes.</param>
    /// <returns>Replayed with the intent, or not found; another tenant's intent is indistinguishable from an absent one.</returns>
    /// <exception cref="ArgumentException"><paramref name="tenantId"/> is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="preparationId"/> is empty.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public ValueTask<ArtifactReferenceCommitIntentResult> GetAsync(TenantId tenantId, ArtifactPreparationId preparationId, CancellationToken cancellationToken = default);

    /// <summary>Conditionally moves an intent to its next state.</summary>
    /// <param name="tenantId">The tenant partition.</param>
    /// <param name="preparationId">The preparation identity.</param>
    /// <param name="expected">The state the caller observed.</param>
    /// <param name="nextState">The state to move to; only the documented transitions are legal.</param>
    /// <param name="at">The transition instant.</param>
    /// <param name="cancellationToken">Cancels before the transition commits.</param>
    /// <returns>Applied, replayed when the intent already reached <paramref name="nextState"/>, conflict for an illegal transition, state changed when another actor won, or not found.</returns>
    /// <exception cref="ArgumentException"><paramref name="tenantId"/> is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The preparation is empty or a state is undefined.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public ValueTask<ArtifactReferenceCommitIntentResult> TransitionAsync(
        TenantId tenantId,
        ArtifactPreparationId preparationId,
        ArtifactReferenceCommitState expected,
        ArtifactReferenceCommitState nextState,
        DateTimeOffset at,
        CancellationToken cancellationToken = default);

    /// <summary>Lists pending intents recorded before an instant, oldest first, for a reconciler's sweep.</summary>
    /// <param name="tenantId">The tenant partition.</param>
    /// <param name="recordedBefore">The exclusive upper bound on <see cref="ArtifactReferenceCommitIntent.RecordedAt"/>.</param>
    /// <param name="limit">The positive maximum number of intents returned.</param>
    /// <param name="cancellationToken">Cancels before the list completes.</param>
    /// <returns>At most <paramref name="limit"/> pending intents ordered by record time then identity.</returns>
    /// <exception cref="ArgumentException"><paramref name="tenantId"/> is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is not positive.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public ValueTask<ImmutableArray<ArtifactReferenceCommitIntent>> ListPendingAsync(
        TenantId tenantId,
        DateTimeOffset recordedBefore,
        int limit,
        CancellationToken cancellationToken = default);
}
