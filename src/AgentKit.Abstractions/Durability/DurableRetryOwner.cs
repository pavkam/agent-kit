// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Declares which component is responsible for retrying one recoverable
/// operation, so that two layers never independently retry the same effect.
/// </summary>
/// <remarks>
/// Retry ownership is declared per operation and is distinct from
/// <see cref="DurableTimeoutOwner"/>: an operation may be retried by its
/// caller while its deadline is enforced by the backend, or the reverse.
/// Ambiguous ownership is the usual cause of duplicated external effects, so
/// exactly one owner is recorded on the durable descriptor.
/// </remarks>
public enum DurableRetryOwner
{
    /// <summary>
    /// The operation is not retried automatically. A failure is terminal
    /// unless an operator or a higher-level decision starts new work.
    /// </summary>
    None,

    /// <summary>
    /// The calling component — for example a provider or tool pipeline —
    /// owns retry, after checking idempotency and side-effect certainty.
    /// </summary>
    Caller,

    /// <summary>
    /// The durable backend owns retry through its own scheduling. Its
    /// settings still may not override the operation's idempotency
    /// classification.
    /// </summary>
    Backend,
}
