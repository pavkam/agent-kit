// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Storage;

/// <summary>Is the tenant-partitioned projection and transition rules every intent-store adapter shares.</summary>
/// <remarks>
/// <para>
/// The table plans each operation without mutating itself: <c>Plan*</c> returns an <see cref="ArtifactReferenceCommitIntentDecision"/>
/// and <see cref="Apply"/> projects the persisted intent. Only <see cref="ArtifactReferenceCommitState.Pending"/> to
/// <see cref="ArtifactReferenceCommitState.Committed"/>, <see cref="ArtifactReferenceCommitState.Pending"/> to
/// <see cref="ArtifactReferenceCommitState.Fenced"/>, and <see cref="ArtifactReferenceCommitState.Fenced"/> to
/// <see cref="ArtifactReferenceCommitState.Collected"/> are legal. The class is not thread-safe; the owning adapter serializes
/// every call, which is what makes a late reference commit and a fence race through one conditional transition.
/// </para>
/// </remarks>
internal sealed class ArtifactReferenceCommitIntentTable
{
    private readonly Dictionary<TenantArtifactPreparationKey, ArtifactReferenceCommitIntent> _intents = [];

    /// <summary>Plans recording an intent: first recording applies, an equal intent replays, another intent conflicts.</summary>
    /// <param name="intent">The non-null intent to record.</param>
    /// <returns>The decision.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="intent"/> is null.</exception>
    internal ArtifactReferenceCommitIntentDecision PlanRecord(ArtifactReferenceCommitIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        var key = new TenantArtifactPreparationKey(intent.TenantId, intent.PreparationId);
        return _intents.TryGetValue(key, out var stored)
            ? new(
                new(SameIntent(stored, intent) ? ArtifactReferenceCommitIntentOutcome.Replayed : ArtifactReferenceCommitIntentOutcome.Conflict, stored),
                null)
            : new(new(ArtifactReferenceCommitIntentOutcome.Applied, intent), intent);
    }

    /// <summary>Reads the stored intent of a tenant preparation.</summary>
    /// <param name="tenantId">The non-blank tenant partition.</param>
    /// <param name="preparationId">The non-empty preparation identity.</param>
    /// <returns>Replayed with the intent, or not found, which is also the answer for another tenant's intent.</returns>
    /// <exception cref="ArgumentException"><paramref name="tenantId"/> is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="preparationId"/> is empty.</exception>
    internal ArtifactReferenceCommitIntentResult Get(TenantId tenantId, ArtifactPreparationId preparationId) =>
        _intents.TryGetValue(new TenantArtifactPreparationKey(tenantId, preparationId), out var stored)
            ? new(ArtifactReferenceCommitIntentOutcome.Replayed, stored)
            : new(ArtifactReferenceCommitIntentOutcome.NotFound, null);

    /// <summary>Plans a conditional transition.</summary>
    /// <param name="tenantId">The non-blank tenant partition.</param>
    /// <param name="preparationId">The non-empty preparation identity.</param>
    /// <param name="expected">The defined state the caller observed.</param>
    /// <param name="nextState">The defined state to move to.</param>
    /// <param name="at">The transition instant; <see cref="ArtifactReferenceCommitIntent.UpdatedAt"/> never moves before the recording instant.</param>
    /// <returns>Applied with the updated intent to persist, replayed when the intent already is in <paramref name="nextState"/>, state changed when another actor won, conflict for an illegal transition, or not found.</returns>
    /// <exception cref="ArgumentException"><paramref name="tenantId"/> is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The preparation is empty or a state is undefined.</exception>
    internal ArtifactReferenceCommitIntentDecision PlanTransition(
        TenantId tenantId,
        ArtifactPreparationId preparationId,
        ArtifactReferenceCommitState expected,
        ArtifactReferenceCommitState nextState,
        DateTimeOffset at)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(expected);
        ArgumentOutOfRangeException.ThrowIfUndefined(nextState);
        if (!_intents.TryGetValue(new TenantArtifactPreparationKey(tenantId, preparationId), out var stored))
        {
            return new(new(ArtifactReferenceCommitIntentOutcome.NotFound, null), null);
        }

        if (stored.State == nextState)
        {
            return new(new(ArtifactReferenceCommitIntentOutcome.Replayed, stored), null);
        }

        if (stored.State != expected)
        {
            return new(new(ArtifactReferenceCommitIntentOutcome.StateChanged, stored), null);
        }

        if (!IsLegal(expected, nextState))
        {
            return new(new(ArtifactReferenceCommitIntentOutcome.Conflict, stored), null);
        }

        var updated = new ArtifactReferenceCommitIntent(
            stored.Id, stored.TenantId, stored.PreparationId, stored.ArtifactId, stored.Version, stored.OwnerId,
            stored.Pin, nextState, stored.RecordedAt, at < stored.RecordedAt ? stored.RecordedAt : at);
        return new(new(ArtifactReferenceCommitIntentOutcome.Applied, updated), updated);
    }

    /// <summary>Lists pending intents of a tenant recorded before an instant, oldest first.</summary>
    /// <param name="tenantId">The non-blank tenant partition.</param>
    /// <param name="recordedBefore">The exclusive upper bound on the recording instant.</param>
    /// <param name="limit">The positive maximum count.</param>
    /// <returns>At most <paramref name="limit"/> intents ordered by recording instant, then identity.</returns>
    /// <exception cref="ArgumentException"><paramref name="tenantId"/> is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is not positive.</exception>
    internal ImmutableArray<ArtifactReferenceCommitIntent> ListPending(TenantId tenantId, DateTimeOffset recordedBefore, int limit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId.Value, nameof(tenantId));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        return
        [
            .. _intents.Values
                .Where(intent => intent.TenantId == tenantId
                    && intent.State == ArtifactReferenceCommitState.Pending
                    && intent.RecordedAt < recordedBefore)
                .OrderBy(static intent => intent.RecordedAt)
                .ThenBy(static intent => intent.Id.Value)
                .Take(limit),
        ];
    }

    /// <summary>Projects an intent a decision persisted.</summary>
    /// <param name="decision">The decision whose <see cref="ArtifactReferenceCommitIntentDecision.Persist"/> value, when present, is now durable.</param>
    internal void Apply(ArtifactReferenceCommitIntentDecision decision)
    {
        if (decision.Persist is { } intent)
        {
            _intents[new TenantArtifactPreparationKey(intent.TenantId, intent.PreparationId)] = intent;
        }
    }

    /// <summary>Restores one persisted intent during recovery; a later restoration of the same preparation replaces the earlier one.</summary>
    /// <param name="intent">The non-null recovered intent.</param>
    /// <exception cref="ArgumentNullException"><paramref name="intent"/> is null.</exception>
    internal void Restore(ArtifactReferenceCommitIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        _intents[new TenantArtifactPreparationKey(intent.TenantId, intent.PreparationId)] = intent;
    }

    /// <summary>Snapshots every held intent for compaction.</summary>
    /// <returns>The intents in deterministic recording order.</returns>
    internal ImmutableArray<ArtifactReferenceCommitIntent> Snapshot() =>
        [.. _intents.Values.OrderBy(static intent => intent.RecordedAt).ThenBy(static intent => intent.Id.Value)];

    private static bool IsLegal(ArtifactReferenceCommitState from, ArtifactReferenceCommitState to) =>
        (from, to) is (ArtifactReferenceCommitState.Pending, ArtifactReferenceCommitState.Committed)
            or (ArtifactReferenceCommitState.Pending, ArtifactReferenceCommitState.Fenced)
            or (ArtifactReferenceCommitState.Fenced, ArtifactReferenceCommitState.Collected);

    private static bool SameIntent(ArtifactReferenceCommitIntent stored, ArtifactReferenceCommitIntent candidate) =>
        stored.Id == candidate.Id
        && stored.TenantId == candidate.TenantId
        && stored.PreparationId == candidate.PreparationId
        && stored.ArtifactId == candidate.ArtifactId
        && stored.Version == candidate.Version
        && stored.OwnerId == candidate.OwnerId;
}
