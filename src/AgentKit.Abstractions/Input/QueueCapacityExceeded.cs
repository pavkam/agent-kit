// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports immediate backpressure when a new admission cannot reserve queue capacity.</summary>
public sealed record QueueCapacityExceeded: InputAdmissionResult
{
    /// <summary>Initializes capacity rejection.</summary><param name="limit">The observed limit.</param><param name="retryAfter">Optional retry guidance.</param>
    public QueueCapacityExceeded(InputCapacityLimit limit, TimeSpan? retryAfter)
    {
        ArgumentNullException.ThrowIfNull(limit);
        if (retryAfter is { } delay)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(delay, TimeSpan.Zero, nameof(retryAfter));
        }
        Limit = limit; RetryAfter = retryAfter;
    }
    /// <summary>Gets capacity evidence.</summary><value>The configured limit and occupancy.</value>
    public InputCapacityLimit Limit { get; }
    /// <summary>Gets retry guidance.</summary><value>A nonnegative delay, or null.</value>
    public TimeSpan? RetryAfter { get; }
}
