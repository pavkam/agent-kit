// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Captures finite canonical terminal-result limits selected before invocation.</summary>
public sealed record ToolResultBounds
{
    /// <summary>Initializes positive canonical-byte and part limits.</summary>
    /// <param name="maximumCanonicalBytes">Positive maximum canonical retained bytes.</param>
    /// <param name="maximumParts">Positive maximum retained parts.</param>
    /// <exception cref="ArgumentOutOfRangeException">A limit is zero or negative.</exception>
    public ToolResultBounds(long maximumCanonicalBytes, int maximumParts)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumCanonicalBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumParts);
        MaximumCanonicalBytes = maximumCanonicalBytes;
        MaximumParts = maximumParts;
    }
    /// <summary>Gets the positive canonical retained-byte ceiling.</summary>
    /// <value>A finite positive byte ceiling.</value>
    public long MaximumCanonicalBytes { get; }

    /// <summary>Gets the positive retained-part ceiling.</summary>
    /// <value>A finite positive part ceiling.</value>
    public int MaximumParts { get; }
}
