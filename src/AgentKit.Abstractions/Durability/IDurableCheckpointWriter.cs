// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The coordinator-owned writer through which a running
/// <see cref="IDurableOperationHandler"/> commits mid-operation durable
/// evidence for the attempt it was invoked for.
/// </summary>
/// <remarks>
/// <para>
/// A handler owns an effect; it does not own durable truth. The journal, the
/// execution lease, the captured authorization, and the checkpoint-identity
/// generator all belong to the coordinator, so the coordinator hands the
/// handler this narrow writer instead of any of those collaborators. A handler
/// therefore cannot write under a fencing generation it never acquired,
/// address a record to another operation, or bypass the authority named by the
/// operation's captured <see cref="DurableExecutionContext.Authorization"/>.
/// </para>
/// <para>
/// An instance is bound to exactly one operation attempt and is valid only for
/// the duration of the <see cref="IDurableOperationHandler.InvokeAsync"/> call
/// it was supplied to. Retaining it past that call, or using it from work that
/// outlives the call, writes under a lease the coordinator may already have
/// released; implementations reject such a write rather than accepting it.
/// </para>
/// <para>
/// Implementations are safe for concurrent use within one attempt, but writes
/// are not ordered relative to each other. A handler that needs ordering must
/// await each write before starting the next, because a checkpoint replaces
/// the operation's complete recorded state.
/// </para>
/// <para>
/// Every method returns a typed <see cref="DurableRecordResult"/> rather than
/// throwing on refusal. A handler that has lost ownership must be able to see
/// <see cref="DurableRecordFenced"/> and stop performing its effect, which an
/// exception-only contract would conflate with an ordinary local fault.
/// </para>
/// </remarks>
public interface IDurableCheckpointWriter
{
    /// <summary>Gets the exact operation attempt this writer commits evidence for.</summary>
    /// <value>
    /// The same binding carried by the descriptor the handler was invoked with. It is exposed so a handler can
    /// assert coherence rather than assuming the writer it received belongs to the operation it is running.
    /// </value>
    public DurableOperationBinding Binding { get; }

    /// <summary>Gets the ownership generation every write from this writer carries.</summary>
    /// <value>
    /// The fencing token of the execution lease the coordinator acquired for this attempt. It is read-only
    /// evidence: a handler cannot substitute another generation.
    /// </value>
    public FencingToken FencingToken { get; }

    /// <summary>
    /// Commits one complete state snapshot at a semantic boundary, replacing
    /// the operation's current recorded state.
    /// </summary>
    /// <param name="kind">
    /// The semantic boundary reached. It must be a defined
    /// <see cref="DurableCheckpointKind"/> member, because recovery dispatches on it.
    /// </param>
    /// <param name="state">
    /// The non-null complete versioned state required to resume from this boundary. It is a total snapshot, not a
    /// delta, and must contain identities and counts rather than prompts, arguments, results, or retrieved content.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels the write. Cancellation before commit leaves no checkpoint; cancellation after commit does not
    /// retract one.
    /// </param>
    /// <returns>
    /// <see cref="DurableRecorded"/> when the checkpoint is durable, <see cref="DurableRecordFenced"/> when this
    /// attempt has lost ownership and must stop, or <see cref="DurableRecordFailed"/> when the write could not be
    /// committed or authorized.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is not a defined enumeration value.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was signalled.</exception>
    public ValueTask<DurableRecordResult> RecordCheckpointAsync(
        DurableCheckpointKind kind,
        OperationPayload state,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits that this attempt is waiting on an external owner, an approval,
    /// or a deferred not-before instant before it may proceed.
    /// </summary>
    /// <param name="condition">
    /// The non-null reason progress stopped, including the truthful side-effect certainty at that instant.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels the write. Cancellation before commit leaves no waiting record; cancellation after commit does not
    /// retract one.
    /// </param>
    /// <returns>
    /// <see cref="DurableRecorded"/> when the waiting state is durable, <see cref="DurableRecordFenced"/> when this
    /// attempt has lost ownership and must stop, or <see cref="DurableRecordFailed"/> when the write could not be
    /// committed or authorized.
    /// </returns>
    /// <remarks>
    /// Recording a wait publishes no outcome and commits no effect. It exists so a process that dies while waiting
    /// leaves evidence naming what would let the operation resume, instead of evidence that merely stops.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="condition"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was signalled.</exception>
    public ValueTask<DurableRecordResult> RecordWaitingAsync(
        DurableWaitCondition condition,
        CancellationToken cancellationToken = default);
}
