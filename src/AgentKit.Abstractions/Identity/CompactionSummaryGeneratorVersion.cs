// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Versions one registered summary generator implementation.</summary>
public readonly record struct CompactionSummaryGeneratorVersion
{
    /// <summary>Initializes a validated <see cref="CompactionSummaryGeneratorVersion"/> value.</summary>
    /// <param name="value">The non-empty canonical text.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is null, empty, or whitespace.</exception>
    public CompactionSummaryGeneratorVersion(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the canonical text.</summary>
    public string Value { get; }

    /// <inheritdoc/>
    public override string ToString() => Value;
}
