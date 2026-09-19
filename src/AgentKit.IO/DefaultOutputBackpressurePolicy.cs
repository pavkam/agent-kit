// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO;

/// <summary>The first-party stateless backpressure policy: always wait for a required sink, always wait for a bounded grace period for a best-effort sink.</summary>
/// <remarks>
/// This policy never returns <see cref="BackpressureDecision.Drop"/> or <see cref="BackpressureDecision.Disconnect"/>
/// for a <see cref="RunEventDelivery.Required"/> sink, since a required sink's contract forbids losing its
/// evidence or silently stopping its delivery. For a <see cref="RunEventDelivery.BestEffort"/> sink it waits
/// until <see cref="MaximumBestEffortWait"/> elapses and then drops the delivery, so one slow observational
/// sink can never block a run indefinitely.
/// </remarks>
internal sealed class DefaultOutputBackpressurePolicy: IOutputBackpressurePolicy
{
    /// <summary>Initializes the policy with an explicit best-effort grace period.</summary>
    /// <param name="maximumBestEffortWait">The positive duration a best-effort delivery may remain blocked before this policy drops it.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maximumBestEffortWait"/> is not positive.</exception>
    public DefaultOutputBackpressurePolicy(TimeSpan maximumBestEffortWait)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maximumBestEffortWait, TimeSpan.Zero);
        MaximumBestEffortWait = maximumBestEffortWait;
    }

    /// <summary>Gets the configured best-effort grace period.</summary>
    /// <value>The positive duration passed to the constructor.</value>
    public TimeSpan MaximumBestEffortWait { get; }

    /// <inheritdoc/>
    public ValueTask<BackpressureDecision> DecideAsync(
        RunEventDelivery delivery, TimeSpan blockedFor, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(delivery);
        ArgumentOutOfRangeException.ThrowIfLessThan(blockedFor, TimeSpan.Zero);
        cancellationToken.ThrowIfCancellationRequested();
        var decision = delivery == RunEventDelivery.Required || blockedFor < MaximumBestEffortWait
            ? BackpressureDecision.Wait
            : BackpressureDecision.Drop;
        return ValueTask.FromResult(decision);
    }
}
