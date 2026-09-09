// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Defines finite payload and part-count ceilings for one projected tool result.</summary>
/// <remarks>The immutable bounds are policy evidence; a future projector enforces them before publishing a projection.</remarks>
public sealed record ToolResultProjectionBounds
{
    /// <summary>Initializes finite projection bounds.</summary>
    /// <param name="maximumBytes">The positive maximum retained payload bytes.</param>
    /// <param name="maximumParts">The positive maximum retained content parts.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maximumBytes"/> or <paramref name="maximumParts"/> is zero or negative.</exception>
    public ToolResultProjectionBounds(long maximumBytes, int maximumParts)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumParts);
        MaximumBytes = maximumBytes;
        MaximumParts = maximumParts;
    }

    /// <summary>Gets the positive maximum number of retained bytes.</summary>
    /// <value>A finite value greater than zero.</value>
    public long MaximumBytes { get; }

    /// <summary>Gets the positive maximum number of retained parts.</summary>
    /// <value>A finite value greater than zero.</value>
    public int MaximumParts { get; }
}
