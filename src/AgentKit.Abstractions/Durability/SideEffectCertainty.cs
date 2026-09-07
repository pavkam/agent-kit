// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// States what is actually known about whether a recoverable operation's
/// external effect happened, which is the single most important input to a
/// safe recovery decision.
/// </summary>
/// <remarks>
/// This is deliberately a three-state fact rather than a boolean.
/// <see cref="Unknown"/> is a real, common, and permanent outcome — a process
/// can die between sending a request and recording its response — and
/// collapsing it into either certainty is how systems silently duplicate
/// payments or silently lose work. "Probably failed" is not a recovery
/// policy.
/// </remarks>
public enum SideEffectCertainty
{
    /// <summary>
    /// The effect definitely did not occur. Evidence proves the operation was
    /// never dispatched, so restarting it duplicates nothing.
    /// </summary>
    DefinitelyNotPerformed,

    /// <summary>
    /// The effect may or may not have occurred. The operation was dispatched
    /// but no terminal evidence was durably recorded. Retrying is safe only
    /// when the effect is idempotent or the owner can be reconciled.
    /// </summary>
    Unknown,

    /// <summary>
    /// The effect definitely occurred and its terminal result is available.
    /// Recovery commits the recorded outcome and must never reinvoke.
    /// </summary>
    DefinitelyPerformed,
}
