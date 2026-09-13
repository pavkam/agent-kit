// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO;

/// <summary>Bounds the process-local live fan-out owned by one run's output publisher.</summary>
/// <remarks>These are subscriber and event-count bounds only. They do not bound event payload bytes, reserve durable sequence ranges, or configure required sink delivery; those remain separate publication responsibilities.</remarks>
public sealed record RunOutputPublisherOptions
{
    /// <summary>Captures positive live fan-out limits before any subscription exists.</summary>
    /// <param name="maximumSubscriptions">The maximum simultaneously registered subscriptions, defaulting to 32.</param>
    /// <param name="capacityPerSubscription">The maximum queued events retained for each subscription, defaulting to 256.</param>
    /// <exception cref="ArgumentOutOfRangeException">Either limit is less than one.</exception>
    public RunOutputPublisherOptions(int maximumSubscriptions = 32, int capacityPerSubscription = 256)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumSubscriptions, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(capacityPerSubscription, 1);
        MaximumSubscriptions = maximumSubscriptions;
        CapacityPerSubscription = capacityPerSubscription;
    }

    /// <summary>Gets the maximum concurrently registered recipients for one run.</summary>
    /// <value>A positive count; a released or disconnected subscription frees its registration immediately.</value>
    public int MaximumSubscriptions { get; }

    /// <summary>Gets the maximum event count retained for each recipient.</summary>
    /// <value>A positive count; a saturated recipient is disconnected explicitly instead of blocking the producer.</value>
    public int CapacityPerSubscription { get; }

    /// <summary>Projects these validated bounds onto the internal hub options.</summary>
    /// <returns>Equivalent hub bounds; no value is widened or defaulted during projection.</returns>
    internal RunEventHubOptions ToHubOptions() => new(MaximumSubscriptions, CapacityPerSubscription);
}
