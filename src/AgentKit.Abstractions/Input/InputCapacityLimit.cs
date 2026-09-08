// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Describes the bounded pending-input capacity that rejected an admission.</summary>
public sealed record InputCapacityLimit
{
    /// <summary>Initializes capacity evidence.</summary>
    /// <param name="maximumPendingInputs">The positive configured capacity.</param><param name="currentPendingInputs">The nonnegative observed occupancy.</param>
    public InputCapacityLimit(int maximumPendingInputs, int currentPendingInputs)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumPendingInputs);
        ArgumentOutOfRangeException.ThrowIfNegative(currentPendingInputs);
        MaximumPendingInputs = maximumPendingInputs; CurrentPendingInputs = currentPendingInputs;
    }
    /// <summary>Gets configured capacity.</summary><value>A positive count.</value>
    public int MaximumPendingInputs { get; }
    /// <summary>Gets observed occupancy.</summary><value>A nonnegative count.</value>
    public int CurrentPendingInputs { get; }
}
