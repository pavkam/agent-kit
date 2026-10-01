// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports how many sinks received one event and how many failed.</summary>
/// <remarks>An observational sink failure never changes the operation. A required sink failure means required observation is unavailable, which fail-closed callers such as the retrieval pipeline treat as a refusal to expose content.</remarks>
public sealed record MemoryEventDispatchResult
{
    /// <summary>Gets the result of dispatching to no sinks.</summary>
    public static MemoryEventDispatchResult None { get; } = new(0, 0, 0);

    /// <summary>Initializes a validated result.</summary>
    /// <param name="delivered">The number of sinks that recorded the event.</param>
    /// <param name="observationalFailures">The number of observational sinks that failed or were unavailable.</param>
    /// <param name="requiredFailures">The number of required sinks that failed or were unavailable.</param>
    /// <exception cref="ArgumentOutOfRangeException">A count is negative.</exception>
    public MemoryEventDispatchResult(int delivered, int observationalFailures, int requiredFailures)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(delivered);
        ArgumentOutOfRangeException.ThrowIfNegative(observationalFailures);
        ArgumentOutOfRangeException.ThrowIfNegative(requiredFailures);
        Delivered = delivered;
        ObservationalFailures = observationalFailures;
        RequiredFailures = requiredFailures;
    }

    /// <summary>Gets the number of sinks that recorded the event.</summary>
    public int Delivered { get; }

    /// <summary>Gets the number of observational sinks that failed or were unavailable.</summary>
    public int ObservationalFailures { get; }

    /// <summary>Gets the number of required sinks that failed or were unavailable.</summary>
    public int RequiredFailures { get; }

    /// <summary>Gets a value indicating whether every required sink recorded the event.</summary>
    public bool RequiredDeliveryComplete => RequiredFailures == 0;
}
