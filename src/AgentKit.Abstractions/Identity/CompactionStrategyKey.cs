// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Identifies which compaction strategy produced a <see cref="CompactionCheckpoint"/>,
/// recorded on <see cref="CompactionProducer"/> for provenance and audit.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural (ordinal,
/// textual) equality over <see cref="Value"/>, safe to share across threads
/// without synchronization.
/// </remarks>
public readonly record struct CompactionStrategyKey
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CompactionStrategyKey"/>
    /// struct, validating that it carries usable key text.
    /// </summary>
    /// <param name="value">The non-empty canonical strategy key text.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    public CompactionStrategyKey(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the canonical strategy key text.</summary>
    public string Value { get; }

    /// <summary>Returns the canonical key text.</summary>
    public override string ToString() => Value;
}
