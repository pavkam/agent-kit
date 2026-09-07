// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Declares what cancelling one recoverable operation actually achieves,
/// distinguishing stopping a local wait from stopping the external effect.
/// </summary>
/// <remarks>
/// Cancellation of durable work is frequently misunderstood as termination.
/// For handed-off operations it is not: cancelling stops local awaiting and
/// lease renewal, and the true external state must then be recorded. This
/// enumeration forces that distinction to be declared rather than assumed.
/// </remarks>
public enum CancellationSemantics
{
    /// <summary>
    /// The operation cannot be cancelled once accepted. A cancellation
    /// request is refused rather than silently ignored.
    /// </summary>
    NotCancellable,

    /// <summary>
    /// The operation can be cancelled only before its effect is dispatched.
    /// After dispatch, cancellation degrades to
    /// <see cref="LocalWaitOnly"/> semantics.
    /// </summary>
    CooperativeBeforeEffect,

    /// <summary>
    /// Cancellation abandons the local wait and lease renewal only. The
    /// external operation continues, and its outcome must be reconciled
    /// rather than assumed cancelled.
    /// </summary>
    LocalWaitOnly,

    /// <summary>
    /// The effect owner supports a genuine cancellation request, and the
    /// operation reports whether that request was accepted before the effect
    /// completed.
    /// </summary>
    ExternallyCancellable,
}
