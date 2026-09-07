// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Declares whether repeating a recoverable operation is safe, and on what
/// basis, so recovery can decide between retrying and requiring
/// reconciliation.
/// </summary>
/// <remarks>
/// This classification is a property of the effect itself and is declared by
/// the component that owns that effect. A durable backend's retry
/// configuration must never override it: a workflow engine willing to retry
/// does not make a non-idempotent charge safe to repeat.
/// </remarks>
public enum IdempotencyClassification
{
    /// <summary>
    /// The operation performs no external mutation, so repeating it is
    /// always safe.
    /// </summary>
    ReadOnly,

    /// <summary>
    /// The operation is naturally idempotent: applying it twice produces the
    /// same observable state as applying it once, without needing a key.
    /// </summary>
    Idempotent,

    /// <summary>
    /// The operation is safely repeatable only when its
    /// <see cref="IdempotencyKey"/> is accepted and honored by the effect
    /// owner, which collapses duplicate attempts into one effect.
    /// </summary>
    IdempotentWithKey,

    /// <summary>
    /// Repeating the operation would duplicate its effect. When the outcome
    /// is <see cref="SideEffectCertainty.Unknown"/>, recovery must reconcile
    /// or escalate to an operator rather than retry.
    /// </summary>
    NonIdempotent,
}
