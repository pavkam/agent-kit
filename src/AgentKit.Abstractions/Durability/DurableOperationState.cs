// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The persisted lifecycle position of one recoverable operation, forming a
/// total dispatcher algebra in which every state has exactly one defined
/// recovery procedure.
/// </summary>
/// <remarks>
/// Every state either advances, waits, terminates, or faults. There is no
/// state from which a recovering worker may simply loop, because a drive step
/// that reports "continue" without committing durable progress is an
/// invariant failure rather than a retry.
/// </remarks>
public enum DurableOperationState
{
    /// <summary>
    /// The operation's immutable metadata and initial state are committed but
    /// no effect has been started. Recovery may start it.
    /// </summary>
    Accepted,

    /// <summary>
    /// The effect has been dispatched and no terminal evidence is recorded.
    /// This is the state whose side-effect certainty is
    /// <see cref="SideEffectCertainty.Unknown"/>.
    /// </summary>
    EffectPending,

    /// <summary>
    /// A complete terminal outcome is durably staged but not yet published in
    /// its required source order. Recovery materializes it and must never
    /// reinvoke the effect.
    /// </summary>
    OutcomeReady,

    /// <summary>
    /// The operation is waiting on an external owner, approval, or a
    /// not-before retry instant. Recovery resumes waiting or re-drives after
    /// the condition is met.
    /// </summary>
    Waiting,

    /// <summary>
    /// The operation finished and its result was published. No further
    /// recovery work is required.
    /// </summary>
    Completed,

    /// <summary>
    /// The operation failed terminally and will not be resumed. The recorded
    /// failure is the final outcome.
    /// </summary>
    Faulted,
}
