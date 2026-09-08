// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Globalization;

/// <summary>Identifies a positive compare-and-swap revision of one execution lane.</summary>
/// <remarks>The revision covers the lane's branch cursor, pending-input state, and current operation ownership. It is independent of both the whole-session version and the accepted operation's total-state revision.</remarks>
public readonly record struct SessionLaneRevision
{
    /// <summary>Initializes an execution-lane revision.</summary>
    /// <param name="value">The positive revision assigned by the session owner.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is zero or negative.</exception>
    public SessionLaneRevision(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        Value = value;
    }

    /// <summary>Gets the positive lane revision.</summary>
    /// <value>A value greater than zero used to reject proposals based on stale lane state.</value>
    public long Value { get; }

    /// <summary>Formats the revision using invariant culture.</summary>
    /// <returns>The decimal representation of <see cref="Value"/>.</returns>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
