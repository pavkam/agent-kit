// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Versions one immutable compaction profile publication.</summary>
public readonly record struct CompactionProfileVersion
{
    /// <summary>Initializes a validated <see cref="CompactionProfileVersion"/> value.</summary>
    /// <param name="value">A positive version number.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is not positive.</exception>
    public CompactionProfileVersion(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        Value = value;
    }

    /// <summary>Gets the version number.</summary>
    public long Value { get; }
}
