// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The durable record of what recoverable operations were accepted, how far
/// they progressed, and how they ended. The journal is the authoritative
/// source of recovery truth.
/// </summary>
/// <remarks>
/// <para>
/// Implementations are used concurrently by many agents, sessions, and runs
/// in one engine and must be thread-safe. They are normally registered as
/// singletons keyed by <see cref="DurableJournalKey"/>.
/// </para>
/// <para>
/// Every write carries the caller's <see cref="FencingToken"/>. An
/// implementation MUST reject a write whose token is older than the current
/// authoritative generation by returning
/// <see cref="DurableRecordFenced"/>, and MUST NOT silently accept it. This
/// is the mechanism that prevents a stale worker from corrupting a resumed
/// run.
/// </para>
/// <para>
/// The journal records; it does not execute, dispatch, retry, or decide.
/// Classification belongs to <see cref="IRecoveryPolicy"/>, so the journal
/// never depends on the coordinator that reads it.
/// </para>
/// <para>
/// Journal access is a protected operation. Implementations authorize through
/// the authority named by the operation's captured
/// <see cref="DurableExecutionContext.Authorization"/> and fail closed when
/// authority or required audit is unavailable.
/// </para>
/// </remarks>
public interface IDurableOperationJournal
{
    /// <summary>
    /// Commits the acceptance record for an operation before any effect
    /// occurs.
    /// </summary>
    /// <param name="start">
    /// The immutable declaration and complete initial state.
    /// </param>
    /// <param name="cancellationToken">
    /// A token that cancels the write. Cancellation before commit leaves no
    /// record; cancellation after commit does not retract one.
    /// </param>
    /// <returns>
    /// <see cref="DurableRecorded"/> when the record is durable,
    /// <see cref="DurableRecordFenced"/> when the caller has lost ownership,
    /// or <see cref="DurableRecordFailed"/> when the store could not commit.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="start"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was signalled.
    /// </exception>
    public ValueTask<DurableRecordResult> RecordStartAsync(
        DurableOperationStart start,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits one complete state snapshot at a semantic boundary, replacing
    /// the operation's current recorded state.
    /// </summary>
    /// <param name="checkpoint">The complete state snapshot.</param>
    /// <param name="cancellationToken">A token that cancels the write.</param>
    /// <returns>
    /// <see cref="DurableRecorded"/> when the checkpoint is durable,
    /// <see cref="DurableRecordFenced"/> when the caller has lost ownership,
    /// or <see cref="DurableRecordFailed"/> when the store could not commit.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="checkpoint"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was signalled.
    /// </exception>
    public ValueTask<DurableRecordResult> RecordCheckpointAsync(
        DurableCheckpoint checkpoint,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits the operation's authoritative terminal record.
    /// </summary>
    /// <param name="result">
    /// The terminal outcome, including truthful side-effect certainty.
    /// </param>
    /// <param name="cancellationToken">A token that cancels the write.</param>
    /// <returns>
    /// <see cref="DurableRecorded"/> when the terminal record is durable,
    /// <see cref="DurableRecordFenced"/> when the caller has lost ownership,
    /// or <see cref="DurableRecordFailed"/> when the store could not commit.
    /// </returns>
    /// <remarks>
    /// Recording a terminal result does not publish it. A staged result whose
    /// source-ordered position is not yet eligible remains
    /// <see cref="DurableOperationState.OutcomeReady"/> and must never cause
    /// reinvocation.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="result"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was signalled.
    /// </exception>
    public ValueTask<DurableRecordResult> RecordTerminalAsync(
        DurableOperationResult result,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads everything durably known about one operation so a recovery
    /// policy can classify it.
    /// </summary>
    /// <param name="address">The operation to load evidence for.</param>
    /// <param name="cancellationToken">A token that cancels the read.</param>
    /// <returns>
    /// <see cref="RecoveryEvidenceLoaded"/> with assembled evidence,
    /// <see cref="RecoveryEvidenceNotFound"/> when no record exists, or
    /// <see cref="RecoveryEvidenceUnavailable"/> when the store could not be
    /// read.
    /// </returns>
    /// <remarks>
    /// This operation is side-effect free. Loading evidence starts no
    /// provider call, tool, retry, timer, or lease renewal; a caller must
    /// explicitly drive any work it decides to resume.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="address"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was signalled.
    /// </exception>
    public ValueTask<RecoveryEvidenceResult> LoadEvidenceAsync(
        DurableOperationAddress address,
        CancellationToken cancellationToken = default);
}
