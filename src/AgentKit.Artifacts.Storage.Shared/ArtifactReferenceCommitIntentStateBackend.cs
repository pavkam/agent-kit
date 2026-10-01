// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Storage;

using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Executes the intent-store contract over an in-memory projection that a durable adapter persists and replays.</summary>
/// <remarks>
/// <para>
/// Every operation runs under one lock, so planning, persistence, and projection are atomic within the process and a late reference
/// commit and a fence race through one conditional transition. A change is persisted before it is projected or acknowledged: a
/// persistence failure propagates to the caller and leaves the projection unchanged, so an acknowledged intent is always durable and a
/// failed write is retried by identity. The adapter replays its persisted intents exactly once before the first operation.
/// </para>
/// <para>Each operation is observed under the shared artifact store activity, log, and bounded metrics with <c>intent_*</c> operation labels; only the adapter, operation, outcome, and tenant identity are recorded.</para>
/// </remarks>
internal abstract class ArtifactReferenceCommitIntentStateBackend: IDisposable
{
    private readonly Lock _lock = new();
    private readonly ArtifactReferenceCommitIntentTable _table = new();
    private readonly string _adapter;
    private readonly TimeProvider _time;
    private bool _initialized;

    /// <summary>Initializes the shared bookkeeping.</summary>
    /// <param name="adapter">The bounded adapter label used in signals.</param>
    /// <param name="time">The clock used only for observational duration.</param>
    /// <param name="logger">The optional content-free logger.</param>
    /// <exception cref="ArgumentException"><paramref name="adapter"/> is blank.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="time"/> is null.</exception>
    protected ArtifactReferenceCommitIntentStateBackend(string adapter, TimeProvider time, ILogger? logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(adapter);
        ArgumentNullException.ThrowIfNull(time);
        _adapter = adapter;
        _time = time;
        Logger = logger ?? NullLogger.Instance;
    }

    /// <summary>Gets the content-free logger.</summary>
    protected ILogger Logger { get; }

    /// <summary>Opens the durable medium and replays every persisted intent, once.</summary>
    /// <param name="cancellationToken">Cancels before initialization completes.</param>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    /// <exception cref="InvalidOperationException">The durable medium cannot be validated safely.</exception>
    internal void Initialize(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_lock)
        {
            EnsureInitialized(cancellationToken);
        }
    }

    /// <summary>Records an intent.</summary>
    /// <param name="intent">The non-null intent.</param>
    /// <param name="cancellationToken">Cancels before the intent is persisted.</param>
    /// <returns>The planned result, durable by the time it is returned.</returns>
    internal ArtifactReferenceCommitIntentResult Record(ArtifactReferenceCommitIntent intent, CancellationToken cancellationToken) =>
        ArtifactStoreObservation.ObserveIntent(
            Logger, _time, _adapter, ArtifactStoreOperationKind.IntentRecord, intent.TenantId,
            () =>
            {
                lock (_lock)
                {
                    EnsureInitialized(cancellationToken);
                    return Commit(_table.PlanRecord(intent), cancellationToken);
                }
            },
            Describe);

    /// <summary>Reads an intent.</summary>
    /// <param name="tenantId">The non-blank tenant partition.</param>
    /// <param name="preparationId">The non-empty preparation identity.</param>
    /// <param name="cancellationToken">Cancels before the read completes.</param>
    /// <returns>Replayed with the intent, or not found.</returns>
    internal ArtifactReferenceCommitIntentResult Get(TenantId tenantId, ArtifactPreparationId preparationId, CancellationToken cancellationToken) =>
        ArtifactStoreObservation.ObserveIntent(
            Logger, _time, _adapter, ArtifactStoreOperationKind.IntentGet, tenantId,
            () =>
            {
                lock (_lock)
                {
                    EnsureInitialized(cancellationToken);
                    return _table.Get(tenantId, preparationId);
                }
            },
            Describe);

    /// <summary>Conditionally transitions an intent.</summary>
    /// <param name="tenantId">The non-blank tenant partition.</param>
    /// <param name="preparationId">The non-empty preparation identity.</param>
    /// <param name="expected">The state the caller observed.</param>
    /// <param name="nextState">The state to move to.</param>
    /// <param name="at">The transition instant.</param>
    /// <param name="cancellationToken">Cancels before the transition is persisted.</param>
    /// <returns>The planned result, durable by the time it is returned.</returns>
    internal ArtifactReferenceCommitIntentResult Transition(
        TenantId tenantId,
        ArtifactPreparationId preparationId,
        ArtifactReferenceCommitState expected,
        ArtifactReferenceCommitState nextState,
        DateTimeOffset at,
        CancellationToken cancellationToken) =>
        ArtifactStoreObservation.ObserveIntent(
            Logger, _time, _adapter, ArtifactStoreOperationKind.IntentTransition, tenantId,
            () =>
            {
                lock (_lock)
                {
                    EnsureInitialized(cancellationToken);
                    return Commit(_table.PlanTransition(tenantId, preparationId, expected, nextState, at), cancellationToken);
                }
            },
            Describe);

    /// <summary>Lists pending intents recorded before an instant.</summary>
    /// <param name="tenantId">The non-blank tenant partition.</param>
    /// <param name="recordedBefore">The exclusive recording-instant bound.</param>
    /// <param name="limit">The positive maximum count.</param>
    /// <param name="cancellationToken">Cancels before the list completes.</param>
    /// <returns>At most <paramref name="limit"/> pending intents, oldest first.</returns>
    internal ImmutableArray<ArtifactReferenceCommitIntent> ListPending(
        TenantId tenantId, DateTimeOffset recordedBefore, int limit, CancellationToken cancellationToken) =>
        ArtifactStoreObservation.ObserveIntent(
            Logger, _time, _adapter, ArtifactStoreOperationKind.IntentListPending, tenantId,
            () =>
            {
                lock (_lock)
                {
                    EnsureInitialized(cancellationToken);
                    return _table.ListPending(tenantId, recordedBefore, limit);
                }
            },
            static _ => ("completed", true));

    /// <summary>Releases the durable medium.</summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Opens the durable medium and restores every persisted intent into <paramref name="table"/>.</summary>
    /// <param name="table">The projection to restore into.</param>
    /// <param name="cancellationToken">Cancels before recovery completes.</param>
    /// <exception cref="InvalidOperationException">The durable medium cannot be validated safely.</exception>
    protected abstract void Recover(ArtifactReferenceCommitIntentTable table, CancellationToken cancellationToken);

    /// <summary>Durably writes one intent, replacing any earlier record of the same tenant preparation.</summary>
    /// <param name="intent">The intent to write.</param>
    /// <param name="cancellationToken">Cancels before the write.</param>
    protected abstract void Persist(ArtifactReferenceCommitIntent intent, CancellationToken cancellationToken);

    /// <summary>Releases adapter-owned resources.</summary>
    /// <param name="disposing"><see langword="true"/> when called from <see cref="Dispose()"/>.</param>
    protected virtual void Dispose(bool disposing)
    {
    }

    private static (string Outcome, bool Succeeded) Describe(ArtifactReferenceCommitIntentResult result) => result.Outcome switch
    {
        ArtifactReferenceCommitIntentOutcome.Applied => ("applied", true),
        ArtifactReferenceCommitIntentOutcome.Replayed => ("replayed", true),
        ArtifactReferenceCommitIntentOutcome.NotFound => ("not_found", true),
        ArtifactReferenceCommitIntentOutcome.StateChanged => ("state_changed", true),
        ArtifactReferenceCommitIntentOutcome.Conflict => ("conflict", false),
        _ => ("unknown", false),
    };

    private ArtifactReferenceCommitIntentResult Commit(ArtifactReferenceCommitIntentDecision decision, CancellationToken cancellationToken)
    {
        if (decision.Persist is { } intent)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                Persist(intent, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                ArtifactStoreObservation.Safe(() => ArtifactStoreLog.PersistFailed(Logger, _adapter, "intent", exception.GetType().Name));
                throw;
            }

            _table.Apply(decision);
        }

        return decision.Result;
    }

    private void EnsureInitialized(CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return;
        }

        Recover(_table, cancellationToken);
        _initialized = true;
    }
}
