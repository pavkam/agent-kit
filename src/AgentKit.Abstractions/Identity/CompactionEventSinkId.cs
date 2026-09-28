// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies one compaction event sink registered for a compactor key.</summary>
public readonly record struct CompactionEventSinkId
{
    /// <summary>Initializes a validated <see cref="CompactionEventSinkId"/> value.</summary>
    /// <param name="value">The non-empty canonical text.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is null, empty, or whitespace.</exception>
    public CompactionEventSinkId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the canonical text.</summary>
    public string Value { get; }

    /// <inheritdoc/>
    public override string ToString() => Value;
}
