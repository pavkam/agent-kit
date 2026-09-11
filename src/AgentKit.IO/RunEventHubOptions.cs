// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO;

/// <summary>Bounds the process-local event buffers owned by one run's hub.</summary>
/// <remarks>These count bounds complement the publisher's payload-byte limits. They do not authorize unbounded event payloads or configure durable storage.</remarks>
internal sealed record RunEventHubOptions
{
    /// <summary>Captures positive limits before any subscription is created.</summary>
    /// <param name="maximumSubscriptions">The maximum simultaneously registered subscriptions, defaulting to 32.</param>
    /// <param name="capacityPerSubscription">The maximum queued events per subscription, defaulting to 256.</param>
    /// <exception cref="ArgumentOutOfRangeException">Either limit is less than one.</exception>
    internal RunEventHubOptions(int maximumSubscriptions = 32, int capacityPerSubscription = 256)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumSubscriptions, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(capacityPerSubscription, 1);
        MaximumSubscriptions = maximumSubscriptions;
        CapacityPerSubscription = capacityPerSubscription;
    }

    /// <summary>Gets the maximum concurrently registered recipients for one run.</summary>
    /// <value>A positive count; disconnected subscriptions release their registration immediately.</value>
    internal int MaximumSubscriptions { get; }

    /// <summary>Gets the maximum event count retained for each recipient.</summary>
    /// <value>A positive count; saturation explicitly disconnects that recipient without blocking the producer.</value>
    internal int CapacityPerSubscription { get; }
}
