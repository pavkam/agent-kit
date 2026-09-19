// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Chooses a <see cref="BackpressureDecision"/> for one run-event delivery attempt that could not proceed immediately.</summary>
/// <remarks>
/// The selected <see cref="IOutputPublisher"/> consults this policy whenever a fan-out buffer for one sink or
/// subscriber is at capacity, or a live subscriber has stopped pulling. The policy is stateless and
/// thread-safe: it observes how long the attempt has already been blocked and the destination's declared
/// <see cref="RunEventDelivery"/>, and it may escalate from <see cref="BackpressureDecision.Wait"/> toward
/// <see cref="BackpressureDecision.Drop"/> or <see cref="BackpressureDecision.Disconnect"/> under a configured
/// deadline rather than blocking indefinitely. It never mutates the run or the blocked event itself.
/// </remarks>
public interface IOutputBackpressurePolicy
{
    /// <summary>Decides how one blocked delivery attempt is handled.</summary>
    /// <param name="delivery">The declared delivery requirement of the blocked destination.</param>
    /// <param name="blockedFor">The non-negative duration this attempt has already been blocked.</param>
    /// <param name="cancellationToken">Cancels the policy's own evaluation.</param>
    /// <returns>The decision the publisher applies to the blocked delivery.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="delivery"/> is undefined, or <paramref name="blockedFor"/> is negative.</exception>
    /// <exception cref="OperationCanceledException">The caller's evaluation wait is cancelled.</exception>
    public ValueTask<BackpressureDecision> DecideAsync(
        RunEventDelivery delivery, TimeSpan blockedFor, CancellationToken cancellationToken = default);
}
