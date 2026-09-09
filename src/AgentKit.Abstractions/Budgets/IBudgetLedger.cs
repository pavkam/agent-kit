// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Persists atomic budget topology, reservations, and accounting facts for one engine composition.</summary>
/// <remarks>
/// <para>
/// Implementations are thread-safe and own no authorization decision. Every
/// supplied scope or reservation reference is an exact address, which the
/// ledger compares before returning data or changing state. A missing and a
/// foreign reference both produce <see cref="BudgetLedgerReferenceUnavailableException"/>
/// so callers cannot probe another tenant's existence.
/// </para>
/// <para>
/// Cancellation before an operation linearizes guarantees that no transition
/// occurs and no read result is disclosed. Cancellation never replaces a
/// known successful result. When the operation may have linearized but its
/// acknowledgement was interrupted, the adapter reports
/// <see cref="BudgetLedgerPersistenceUnavailableException"/> with
/// <see cref="BudgetLedgerPersistenceUnavailableException.AcknowledgementUnknown"/>
/// set to <see langword="true"/>; callers then retry only the exact immutable
/// request. A false acknowledgement-unknown value guarantees that no
/// transition committed. Reference-unavailable, mutation-conflict, and state
/// exceptions also guarantee no transition.
/// </para>
/// </remarks>
public interface IBudgetLedger
{
    /// <summary>Creates a scope or returns its prior receipt for an exact idempotency replay.</summary>
    /// <param name="request">The non-null original scope request and admission facts persisted with a newly created scope.</param>
    /// <param name="cancellationToken">Cancels before operation linearization; cancellation after a possible commit requires an exact replay.</param>
    /// <returns>The persisted scope reference or a typed admission rejection that created no scope.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Cancellation is observed before the operation linearizes; no scope was created.</exception>
    /// <exception cref="BudgetLedgerReferenceUnavailableException">A referenced parent scope is missing or foreign; no scope was created.</exception>
    /// <exception cref="BudgetLedgerMutationConflictException">The original idempotency key is already bound to different immutable scope evidence; no scope was created.</exception>
    /// <exception cref="BudgetLedgerStateException">The parent cannot admit this child in its persisted state; no scope was created.</exception>
    /// <exception cref="BudgetLedgerPersistenceUnavailableException">The adapter cannot confirm the outcome; inspect acknowledgement state before retrying.</exception>
    public ValueTask<BudgetLedgerScopeCreateResult> CreateScopeAsync(BudgetLedgerScopeCreateRequest request, CancellationToken cancellationToken = default);

    /// <summary>Atomically reserves every original request or returns one limit rejection.</summary>
    /// <remarks>
    /// The ledger finds an item-key replay and compares the complete ordered
    /// original batch before reading its clock, sweeping expiry, allocating an
    /// identity, or evaluating capacity. For a new batch it resolves every
    /// null expiry exactly once from the scope's captured admission lifetime
    /// and injected clock, then persists that effective expiry in the receipt.
    /// </remarks>
    /// <param name="request">The non-null exact scope reference and ordered original requests.</param>
    /// <param name="cancellationToken">Cancels before operation linearization; cancellation after a possible commit requires an exact replay.</param>
    /// <returns>Persisted receipts in original request order or a limit rejection that reserved no batch member.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Cancellation is observed before the operation linearizes; no reservation was created.</exception>
    /// <exception cref="BudgetLedgerReferenceUnavailableException">The scope is missing or foreign; no reservation was created.</exception>
    /// <exception cref="BudgetLedgerMutationConflictException">An item key is bound to different ordered original evidence; no reservation was created.</exception>
    /// <exception cref="BudgetLedgerStateException">The scope cannot accept reservations in its persisted state; no reservation was created.</exception>
    /// <exception cref="BudgetLedgerPersistenceUnavailableException">The adapter cannot confirm the outcome; inspect acknowledgement state before retrying.</exception>
    public ValueTask<BudgetLedgerBatchReserveResult> ReserveBatchAsync(BudgetLedgerBatchReserveRequest request, CancellationToken cancellationToken = default);

    /// <summary>Persists start accounting before the caller begins the controlled external effect.</summary>
    /// <remarks>
    /// A first successful transition records a ledger-clock start permission
    /// strictly before the reservation expiry. An exact retry of a started,
    /// unsettled reservation returns <see cref="BudgetStarted"/> with
    /// <see cref="BudgetStarted.WasAlreadyStarted"/> set to
    /// <see langword="true"/>. It never starts an effect itself or renews a
    /// start permission after settlement or reconciliation release. Expiry
    /// returns an exactly replayable <see cref="BudgetStartExpired"/> carrying
    /// the persisted deadline rather than fabricated dimension-limit evidence.
    /// </remarks>
    /// <param name="reservation">The non-null exact persisted reservation locator.</param>
    /// <param name="cancellationToken">Cancels before operation linearization; cancellation after a possible commit requires an exact replay.</param>
    /// <returns>A persisted start permission or ordinary typed budget rejection that forbids the external effect.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reservation"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Cancellation is observed before the operation linearizes; no start permission was persisted.</exception>
    /// <exception cref="BudgetLedgerReferenceUnavailableException">The reservation is missing or foreign; no transition occurred.</exception>
    /// <exception cref="BudgetLedgerStateException">The reservation has an invalid persisted lifecycle state not represented by <see cref="BudgetStartRejected"/> or <see cref="BudgetStartExpired"/>; no transition occurred.</exception>
    /// <exception cref="BudgetLedgerPersistenceUnavailableException">The adapter cannot confirm the outcome; inspect acknowledgement state before retrying.</exception>
    public ValueTask<BudgetStartResult> MarkStartedAsync(BudgetLedgerReservationReference reservation, CancellationToken cancellationToken = default);

    /// <summary>Settles a started reservation with known actual usage.</summary>
    /// <remarks>
    /// The exact reservation and actual usage identify settlement replay. A
    /// repeat with equal values returns the original commitment; a different
    /// actual conflicts and makes no transition.
    /// </remarks>
    /// <param name="request">The non-null exact reservation and nonnegative actual usage.</param>
    /// <param name="cancellationToken">Cancels before operation linearization; cancellation after a possible commit requires an exact replay.</param>
    /// <returns>The persisted commitment result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Cancellation is observed before the operation linearizes; no settlement occurred.</exception>
    /// <exception cref="BudgetLedgerReferenceUnavailableException">The reservation is missing or foreign; no transition occurred.</exception>
    /// <exception cref="BudgetLedgerMutationConflictException">The reservation was settled with different actual usage; no transition occurred.</exception>
    /// <exception cref="BudgetLedgerStateException">The reservation has not started or cannot settle in its persisted state; no transition occurred.</exception>
    /// <exception cref="BudgetLedgerPersistenceUnavailableException">The adapter cannot confirm the outcome; inspect acknowledgement state before retrying.</exception>
    public ValueTask<BudgetCommitResult> SettleAsync(BudgetLedgerSettlementRequest request, CancellationToken cancellationToken = default);

    /// <summary>Releases unstarted capacity, retains started unknown spend, or reports already settled accounting.</summary>
    /// <remarks>
    /// Repeating this operation observes the persisted lifecycle state: an
    /// unstarted reservation is released, a started reservation remains
    /// retained for reconciliation, and a settled reservation returns its
    /// settled no-op receipt. It never refunds a started reservation.
    /// </remarks>
    /// <param name="reservation">The non-null exact persisted reservation locator.</param>
    /// <param name="cancellationToken">Cancels before operation linearization; cancellation after a possible release requires an exact retry.</param>
    /// <returns>The terminal release observation for the exact reservation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reservation"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Cancellation is observed before the operation linearizes; no release occurred.</exception>
    /// <exception cref="BudgetLedgerReferenceUnavailableException">The reservation is missing or foreign; no transition occurred.</exception>
    /// <exception cref="BudgetLedgerPersistenceUnavailableException">The adapter cannot confirm the outcome; inspect acknowledgement state before retrying.</exception>
    public ValueTask<BudgetLedgerReleaseResult> ReleaseUnstartedAsync(BudgetLedgerReservationReference reservation, CancellationToken cancellationToken = default);

    /// <summary>Replaces settled accounting with an authoritative correction.</summary>
    /// <remarks>
    /// The exact reservation, revision, and corrected actual identify replay.
    /// Repeating those values returns the stored correction; reusing the
    /// revision with different usage conflicts and makes no transition.
    /// </remarks>
    /// <param name="request">The non-null exact reservation, nonnegative replacement usage, and positive revision.</param>
    /// <param name="cancellationToken">Cancels before operation linearization; cancellation after a possible commit requires an exact replay.</param>
    /// <returns>The persisted correction result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Cancellation is observed before the operation linearizes; no correction occurred.</exception>
    /// <exception cref="BudgetLedgerReferenceUnavailableException">The reservation is missing or foreign; no transition occurred.</exception>
    /// <exception cref="BudgetLedgerMutationConflictException">The correction revision is bound to different usage; no transition occurred.</exception>
    /// <exception cref="BudgetLedgerStateException">The reservation is not settled or cannot accept the revision; no transition occurred.</exception>
    /// <exception cref="BudgetLedgerPersistenceUnavailableException">The adapter cannot confirm the outcome; inspect acknowledgement state before retrying.</exception>
    public ValueTask<BudgetCorrectionResult> CorrectAsync(BudgetLedgerCorrectionRequest request, CancellationToken cancellationToken = default);

    /// <summary>Reads a point-in-time aggregate snapshot for one exact scope.</summary>
    /// <remarks>
    /// The adapter atomically sweeps expired unstarted reservations before
    /// producing the snapshot. That cleanup is a transition: if its
    /// acknowledgement is unknown, the persistence exception reports
    /// <see cref="BudgetLedgerPersistenceUnavailableException.AcknowledgementUnknown"/>
    /// as <see langword="true"/>; when false, it guarantees neither cleanup
    /// nor another transition committed.
    /// </remarks>
    /// <param name="scope">The non-null exact persisted scope locator.</param>
    /// <param name="cancellationToken">Cancels before the read and cleanup linearize; cancellation never replaces a known snapshot.</param>
    /// <returns>The ledger-clock snapshot after its atomic unstarted-expiry sweep.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="scope"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Cancellation is observed before the read linearizes; no snapshot was disclosed.</exception>
    /// <exception cref="BudgetLedgerReferenceUnavailableException">The scope is missing or foreign; no snapshot was disclosed.</exception>
    /// <exception cref="BudgetLedgerPersistenceUnavailableException">The adapter cannot confirm the snapshot or its preceding cleanup; inspect acknowledgement state before retrying.</exception>
    public ValueTask<BudgetSnapshot> GetSnapshotAsync(BudgetLedgerScopeReference scope, CancellationToken cancellationToken = default);

    /// <summary>Reads one bounded, watermark-anchored page of unresolved started reservations.</summary>
    /// <remarks>
    /// The first page receives a ledger watermark. Later pages must preserve
    /// that watermark and exact scope, use canonical reservation identity
    /// order, and may omit rows settled concurrently. The configured ledger
    /// boundary, rather than this public request type, rejects a page size
    /// above its finite capability.
    /// </remarks>
    /// <param name="query">The non-null exact scope, positive page size, and optional prior cursor.</param>
    /// <param name="cancellationToken">Cancels before the read linearizes; cancellation never replaces a known page.</param>
    /// <returns>The bounded page and continuation cursor when later rows may remain.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Cancellation is observed before the read linearizes; no rows were disclosed.</exception>
    /// <exception cref="BudgetLedgerReferenceUnavailableException">The scope is missing or foreign; no rows were disclosed.</exception>
    /// <exception cref="BudgetLedgerStateException">The cursor watermark or scan state is unusable, or <paramref name="query"/> requests more rows than the scope's captured finite page cap; no rows were disclosed.</exception>
    /// <exception cref="BudgetLedgerPersistenceUnavailableException">The adapter cannot confirm the read outcome; no mutation occurred.</exception>
    public ValueTask<BudgetUnresolvedReservationPage> ReadUnresolvedStartedAsync(BudgetUnresolvedReservationQuery query, CancellationToken cancellationToken = default);

    /// <summary>Records evidence-based reconciliation for unresolved started usage.</summary>
    /// <remarks>
    /// An exact previously recorded reconciliation idempotency key and evidence
    /// replays its stored result, including after settlement or release. Fresh
    /// evidence against an already released or settled reservation fails with
    /// a typed conflict or state exception; it never replaces accounting.
    /// Measured and estimated evidence settles once, proof releases once, and
    /// unknown evidence retains the reservation.
    /// </remarks>
    /// <param name="request">The non-null exact reservation, closed evidence, and idempotency key.</param>
    /// <param name="cancellationToken">Cancels before operation linearization; cancellation after a possible transition requires exact replay.</param>
    /// <returns>The persisted reconciliation result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Cancellation is observed before the operation linearizes; no reconciliation transition occurred.</exception>
    /// <exception cref="BudgetLedgerReferenceUnavailableException">The reservation is missing or foreign; no transition occurred.</exception>
    /// <exception cref="BudgetLedgerMutationConflictException">The reconciliation key is bound to different evidence; no transition occurred.</exception>
    /// <exception cref="BudgetLedgerStateException">Fresh evidence targets an already released or settled reservation; no transition occurred.</exception>
    /// <exception cref="BudgetLedgerPersistenceUnavailableException">The adapter cannot confirm the outcome; inspect acknowledgement state before retrying.</exception>
    public ValueTask<BudgetLedgerReconciliationResult> ReconcileAsync(BudgetLedgerReconciliationRequest request, CancellationToken cancellationToken = default);
}
