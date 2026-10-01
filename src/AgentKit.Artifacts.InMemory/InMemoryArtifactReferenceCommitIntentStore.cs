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
    private readonly Dictionary<TenantArtifactPreparationKey, ArtifactReferenceCommitIntent> _intents = [];

    /// <inheritdoc/>
    public ValueTask<ArtifactReferenceCommitIntentResult> RecordAsync(ArtifactReferenceCommitIntent intent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(intent);
        cancellationToken.ThrowIfCancellationRequested();
        var key = new TenantArtifactPreparationKey(intent.TenantId, intent.PreparationId);
        lock (_lock)
        {
            if (!_intents.TryGetValue(key, out var stored))
            {
                _intents.Add(key, intent);
                return Result(ArtifactReferenceCommitIntentOutcome.Applied, intent);
            }

            return Result(
                SameIntent(stored, intent) ? ArtifactReferenceCommitIntentOutcome.Replayed : ArtifactReferenceCommitIntentOutcome.Conflict,
                stored);
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
            return _intents.TryGetValue(new TenantArtifactPreparationKey(tenantId, preparationId), out var stored)
                ? Result(ArtifactReferenceCommitIntentOutcome.Replayed, stored)
                : Result(ArtifactReferenceCommitIntentOutcome.NotFound, null);
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
        var key = new TenantArtifactPreparationKey(tenantId, preparationId);
        lock (_lock)
        {
            if (!_intents.TryGetValue(key, out var stored))
            {
                return Result(ArtifactReferenceCommitIntentOutcome.NotFound, null);
            }

            if (stored.State == nextState)
            {
                return Result(ArtifactReferenceCommitIntentOutcome.Replayed, stored);
            }

            if (stored.State != expected)
            {
                return Result(ArtifactReferenceCommitIntentOutcome.StateChanged, stored);
            }

            if (!IsLegal(expected, nextState))
            {
                return Result(ArtifactReferenceCommitIntentOutcome.Conflict, stored);
            }

            var updated = new ArtifactReferenceCommitIntent(
                stored.Id, stored.TenantId, stored.PreparationId, stored.ArtifactId, stored.Version, stored.OwnerId,
                stored.Pin, nextState, stored.RecordedAt, at < stored.RecordedAt ? stored.RecordedAt : at);
            _intents[key] = updated;
            return Result(ArtifactReferenceCommitIntentOutcome.Applied, updated);
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
            return ValueTask.FromResult<ImmutableArray<ArtifactReferenceCommitIntent>>(
            [
                .. _intents.Values
                    .Where(intent => intent.TenantId == tenantId
                        && intent.State == ArtifactReferenceCommitState.Pending
                        && intent.RecordedAt < recordedBefore)
                    .OrderBy(static intent => intent.RecordedAt)
                    .ThenBy(static intent => intent.Id.Value)
                    .Take(limit),
            ]);
        }
    }

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

    private static ValueTask<ArtifactReferenceCommitIntentResult> Result(
        ArtifactReferenceCommitIntentOutcome outcome,
        ArtifactReferenceCommitIntent? intent) =>
        ValueTask.FromResult(new ArtifactReferenceCommitIntentResult(outcome, intent));
}
