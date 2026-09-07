// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Bounds local schema or candidate processing before evaluation consumes unbounded resources.</summary>
public sealed record OutputSchemaProcessingLimits
{
    /// <summary>Initializes processing limits.</summary>
    /// <param name="maximumUtf8Bytes">The positive maximum UTF-8 byte count.</param>
    /// <param name="maximumDepth">The positive maximum JSON nesting depth.</param>
    /// <param name="maximumNodes">The positive maximum JSON node count.</param>
    /// <exception cref="ArgumentOutOfRangeException">Any supplied limit is not positive.</exception>
    public OutputSchemaProcessingLimits(int maximumUtf8Bytes, int maximumDepth, int maximumNodes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumUtf8Bytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumDepth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumNodes);
        MaximumUtf8Bytes = maximumUtf8Bytes;
        MaximumDepth = maximumDepth;
        MaximumNodes = maximumNodes;
    }

    /// <summary>Gets the maximum UTF-8 byte count.</summary>
    public int MaximumUtf8Bytes { get; }
    /// <summary>Gets the maximum JSON nesting depth.</summary>
    public int MaximumDepth { get; }
    /// <summary>Gets the maximum JSON node count.</summary>
    public int MaximumNodes { get; }
}
