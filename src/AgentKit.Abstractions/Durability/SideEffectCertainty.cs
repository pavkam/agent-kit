// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// States what is actually known about whether a recoverable operation's
/// external effect happened, which is the single most important input to a
/// safe recovery decision.
/// </summary>
/// <remarks>
/// This is deliberately a five-state fact rather than a boolean.
/// <see cref="Unknown"/> is a real, common, and permanent outcome — a process
/// can die between sending a request and recording its response — and
/// collapsing it into either certainty is how systems silently duplicate
/// payments or silently lose work. "Probably failed" is not a recovery
/// policy.
/// </remarks>
public enum SideEffectCertainty
{
    /// <summary>
    /// The relevant effect definitely did not occur. This requires affirmative
    /// evidence, rather than merely the absence of a dispatch record.
    /// </summary>
    DefinitelyNotPerformed = 0,

    /// <summary>
    /// No completion certainty exists for the relevant effect. Retrying is safe
    /// only when the effect is idempotent or the owner can be reconciled.
    /// </summary>
    Unknown = 1,

    /// <summary>
    /// The relevant effect definitely completed. This does not prove its
    /// terminal result was durably recorded; recording evidence is separate.
    /// </summary>
    DefinitelyPerformed = 2,

    /// <summary>
    /// The relevant external effect affirmatively completed only in part.
    /// Retrying requires the same idempotency or reconciliation protections as
    /// an unknown effect because repeating the operation can duplicate its
    /// completed portion.
    /// </summary>
    PartiallyPerformed = 3,

    /// <summary>
    /// The reported operation has no relevant external-effect boundary. This
    /// is not a synonym for a failed or unstarted effect.
    /// </summary>
    NotApplicable = 4,
}
