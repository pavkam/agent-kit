// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Defines enforceable duration, output, and termination bounds for one process.</summary>
public sealed record ProcessResourceLimits
{
    /// <summary>Initializes bounded process limits.</summary>
    /// <param name="timeout">The finite positive total operation timeout.</param>
    /// <param name="maximumOutputBytes">The positive retained-byte bound across each output stream.</param>
    /// <param name="terminationGracePeriod">The non-negative graceful termination window before forced tree kill.</param>
    /// <exception cref="ArgumentOutOfRangeException">A duration or byte bound is invalid.</exception>
    public ProcessResourceLimits(
        TimeSpan timeout,
        long maximumOutputBytes,
        TimeSpan terminationGracePeriod)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumOutputBytes);
        ArgumentOutOfRangeException.ThrowIfLessThan(terminationGracePeriod, TimeSpan.Zero);
        Timeout = timeout;
        MaximumOutputBytes = maximumOutputBytes;
        TerminationGracePeriod = terminationGracePeriod;
    }

    /// <summary>Gets the finite total operation timeout.</summary>
    public TimeSpan Timeout { get; }

    /// <summary>Gets the retained-byte bound independently applied to stdout and stderr.</summary>
    public long MaximumOutputBytes { get; }

    /// <summary>Gets the graceful termination window before forced process-tree kill.</summary>
    public TimeSpan TerminationGracePeriod { get; }
}
