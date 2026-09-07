// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Declares which component enforces one recoverable operation's deadline, so
/// that a local timeout is never mistaken for proof that external work
/// stopped.
/// </summary>
/// <remarks>
/// This is intentionally separate from <see cref="DurableRetryOwner"/>. The
/// two coincide in many compositions but answer different questions, and
/// merging them would prevent expressing the common case where a caller
/// abandons its local wait while the backend continues to own both the
/// deadline and the running operation.
/// </remarks>
public enum DurableTimeoutOwner
{
    /// <summary>
    /// No deadline is enforced by AgentKit. The operation completes when the
    /// effect owner completes it.
    /// </summary>
    None,

    /// <summary>
    /// The calling component enforces the deadline. Expiry stops the local
    /// wait and produces an outcome whose side-effect certainty may be
    /// <see cref="SideEffectCertainty.Unknown"/>.
    /// </summary>
    Caller,

    /// <summary>
    /// The durable backend enforces the deadline and reports a truthful
    /// terminal state for the operation it owns.
    /// </summary>
    Backend,
}
