// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports immediate typed backpressure when a new admission cannot reserve pending-input capacity.</summary>
/// <remarks>The outcome does not drop, replace, or durably admit the attempted input. Retry guidance is advisory and does not reserve a future queue slot.</remarks>
public sealed record QueueCapacityExceeded: InputAdmissionResult
{
    /// <summary>Initializes a queue-capacity admission outcome.</summary>
    /// <param name="limit">The non-null capacity evidence observed when the requested admission could not reserve a pending slot.</param>
    /// <param name="retryAfter">Optional non-negative retry guidance, or <see langword="null"/> when no delay is suggested.</param>
    /// <exception cref="ArgumentNullException"><paramref name="limit"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="retryAfter"/> is negative.</exception>
    public QueueCapacityExceeded(InputCapacityLimit limit, TimeSpan? retryAfter)
    {
        ArgumentNullException.ThrowIfNull(limit);
        if (retryAfter is { } delay)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(delay, TimeSpan.Zero, nameof(retryAfter));
        }
        Limit = limit; RetryAfter = retryAfter;
    }
    /// <summary>Gets the capacity evidence observed during the failed reservation.</summary>
    /// <value>A non-null point-in-time limit and occupancy observation, not a reservation or future guarantee.</value>
    public InputCapacityLimit Limit { get; }
    /// <summary>Gets optional advisory delay before attempting a new admission.</summary>
    /// <value>A non-negative delay, or <see langword="null"/>; observing the delay does not reserve capacity.</value>
    public TimeSpan? RetryAfter { get; }
}
