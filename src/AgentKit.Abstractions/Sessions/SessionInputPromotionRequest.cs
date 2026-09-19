// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Requests one atomic promotion of a durably admitted selection into an already-accepted run's history.</summary>
/// <remarks>
/// This is the store-level counterpart to <see cref="SessionRunStartRequest"/> for a lane that already carries an
/// accepted run: it materializes the selected admissions into the run's current turn and marks each one promoted,
/// atomically with revalidating the exact lane revision, session version, branch cursor, and operation-state
/// revision the caller observed. It never installs, replaces, or releases accepted run state, and it never selects
/// input itself; the caller supplies an exact selection discovered through <see cref="SessionPendingInputsRequest"/>.
/// </remarks>
public sealed record SessionInputPromotionRequest
{
    /// <summary>Initializes an atomic mid-run promotion proposal.</summary>
    /// <param name="context">The lane-bound in-run context of the accepted operation receiving promotion.</param>
    /// <param name="selectedAdmissionIds">The nonempty, uniquely ordered admissions proposed for promotion.</param>
    /// <param name="cutoffSequence">The inclusive durable admission cutoff the selection was formed under.</param>
    /// <param name="expectedLaneRevision">The lane revision observed when the selection was formed.</param>
    /// <param name="expectedVersion">The canonical whole-session version observed when the selection was formed.</param>
    /// <param name="branchCursor">The exact observed lane-owned branch tip.</param>
    /// <param name="promotionEntryId">The reserved promotion-marker entry identity.</param>
    /// <param name="entryIds">One reserved history-entry identity per selected admission, in selection order.</param>
    /// <param name="messageIds">One reserved message identity per selected admission, in selection order.</param>
    /// <param name="expectedStateRevision">The positive total-state revision currently installed on the accepted run.</param>
    /// <param name="promotedAt">The deterministic commit timestamp.</param>
    /// <param name="idempotencyKey">The exact commit idempotency key.</param>
    /// <exception cref="ArgumentNullException">A required reference is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An identity or revision is default.</exception>
    /// <exception cref="ArgumentException">The context, selection, or reserved-identity evidence is inconsistent.</exception>
    public SessionInputPromotionRequest(
        SessionOperationContext context,
        ImmutableArray<AdmissionId> selectedAdmissionIds,
        SessionSequence cutoffSequence,
        SessionLaneRevision expectedLaneRevision,
        SessionVersion expectedVersion,
        SessionBranchCursor branchCursor,
        SessionEntryId promotionEntryId,
        ImmutableArray<SessionEntryId> entryIds,
        ImmutableArray<MessageId> messageIds,
        OperationStateRevision expectedStateRevision,
        DateTimeOffset promotedAt,
        IdempotencyKey idempotencyKey)
    {
        ArgumentException.ThrowIfSessionContextNotInRun(context);
        if (((InRunOperationCorrelation) context.Correlation).TurnId is null)
        {
            throw new ArgumentException("Input promotion requires a context scoped to a specific turn.", nameof(context));
        }

        ArgumentException.ThrowIfInvalidPromotionAdmissions(selectedAdmissionIds);
        ArgumentOutOfRangeException.ThrowIfEqual(expectedLaneRevision, default);
        ArgumentNullException.ThrowIfNull(branchCursor);
        ArgumentOutOfRangeException.ThrowIfEqual(promotionEntryId, default);
        ArgumentException.ThrowIfDefaultEmptyOrDuplicate(entryIds);
        ArgumentException.ThrowIfDefaultEmptyOrDuplicate(messageIds);
        ArgumentException.ThrowIfNotEqual(entryIds.Length, selectedAdmissionIds.Length, nameof(entryIds));
        ArgumentException.ThrowIfNotEqual(messageIds.Length, selectedAdmissionIds.Length, nameof(messageIds));
        ArgumentException.ThrowIfNotEqual(entryIds.Contains(promotionEntryId), false, nameof(entryIds));
        ArgumentOutOfRangeException.ThrowIfEqual(expectedStateRevision, default);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey.Value, nameof(idempotencyKey));
        Context = context;
        SelectedAdmissionIds = selectedAdmissionIds;
        CutoffSequence = cutoffSequence;
        ExpectedLaneRevision = expectedLaneRevision;
        ExpectedVersion = expectedVersion;
        BranchCursor = branchCursor;
        PromotionEntryId = promotionEntryId;
        EntryIds = entryIds;
        MessageIds = messageIds;
        ExpectedStateRevision = expectedStateRevision;
        PromotedAt = promotedAt;
        IdempotencyKey = idempotencyKey;
    }

    /// <summary>Gets the exact protected in-run context.</summary>
    /// <value>A lane-bound in-run context naming the accepted operation receiving promotion.</value>
    public SessionOperationContext Context { get; }

    /// <summary>Gets the selected admissions proposed for promotion.</summary>
    /// <value>The nonempty, uniquely ordered selection; the store revalidates every entry is still pending.</value>
    public ImmutableArray<AdmissionId> SelectedAdmissionIds { get; }

    /// <summary>Gets the inclusive durable admission cutoff the selection was formed under.</summary>
    /// <value>Every selected admission's durable sequence must be at or before this cutoff.</value>
    public SessionSequence CutoffSequence { get; }

    /// <summary>Gets the lane revision observed when the selection was formed.</summary>
    /// <value>The positive revision revalidated before the atomic transition commits.</value>
    public SessionLaneRevision ExpectedLaneRevision { get; }

    /// <summary>Gets the canonical whole-session version observed when the selection was formed.</summary>
    /// <value>Whole-session optimistic-concurrency evidence checked after idempotency reconciliation.</value>
    public SessionVersion ExpectedVersion { get; }

    /// <summary>Gets the exact observed lane-owned branch tip.</summary>
    /// <value>The tip that must still belong to this lane when the transition commits.</value>
    public SessionBranchCursor BranchCursor { get; }

    /// <summary>Gets the reserved promotion-marker entry identity.</summary>
    /// <value>The non-default identity committed before the materialized message entries.</value>
    public SessionEntryId PromotionEntryId { get; }

    /// <summary>Gets the reserved history-entry identities.</summary>
    /// <value>One identity per selected admission, in selection order.</value>
    public ImmutableArray<SessionEntryId> EntryIds { get; }

    /// <summary>Gets the reserved message identities.</summary>
    /// <value>One identity per selected admission, in selection order.</value>
    public ImmutableArray<MessageId> MessageIds { get; }

    /// <summary>Gets the total-state revision currently installed on the accepted run.</summary>
    /// <value>The positive revision revalidated before commit; the store installs its successor on success.</value>
    public OperationStateRevision ExpectedStateRevision { get; }

    /// <summary>Gets the deterministic commit timestamp.</summary>
    public DateTimeOffset PromotedAt { get; }

    /// <summary>Gets the exact commit idempotency key.</summary>
    public IdempotencyKey IdempotencyKey { get; }

    /// <summary>Gets the run receiving the promoted input.</summary>
    /// <value>The run from the in-run correlation.</value>
    public RunId RunId => ((InRunOperationCorrelation) Context.Correlation).RunId;

    /// <summary>Gets the turn receiving the promoted input.</summary>
    /// <value>The current turn from the in-run correlation.</value>
    public TurnId TargetTurnId => ((InRunOperationCorrelation) Context.Correlation).TurnId!.Value;
}
