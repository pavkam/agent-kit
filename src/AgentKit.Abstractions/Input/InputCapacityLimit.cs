// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Describes the bounded pending-input capacity observed when an admission could not reserve a slot.</summary>
/// <remarks>The values are a point-in-time backpressure observation. They do not reserve capacity, promise that a later retry will succeed, or identify queued payloads.</remarks>
public sealed record InputCapacityLimit
{
    /// <summary>Initializes queue-capacity evidence.</summary>
    /// <param name="maximumPendingInputs">The positive configured maximum number of pending inputs in the applicable capacity scope.</param>
    /// <param name="currentPendingInputs">The non-negative occupancy observed when capacity was evaluated.</param>
    /// <exception cref="ArgumentOutOfRangeException">Capacity is not positive or occupancy is negative.</exception>
    public InputCapacityLimit(int maximumPendingInputs, int currentPendingInputs)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumPendingInputs);
        ArgumentOutOfRangeException.ThrowIfNegative(currentPendingInputs);
        MaximumPendingInputs = maximumPendingInputs; CurrentPendingInputs = currentPendingInputs;
    }
    /// <summary>Gets the configured maximum number of pending inputs.</summary>
    /// <value>A positive limit for the relevant queue scope.</value>
    public int MaximumPendingInputs { get; }
    /// <summary>Gets the pending-input occupancy observed during capacity evaluation.</summary>
    /// <value>A non-negative point-in-time count that may change before a later retry.</value>
    public int CurrentPendingInputs { get; }
}
