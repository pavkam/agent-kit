// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies the exact deterministic chunking algorithm and configuration that produced a chunk set.</summary>
/// <remarks>Chunk identity derives from source content, source version, and this value, so a different chunker can never leave stale and current chunks active as one source. Values are preserved exactly without trimming and compare with ordinal, case-sensitive semantics.</remarks>
public readonly record struct ChunkerVersion
{
    /// <summary>Initializes a non-blank chunker version.</summary>
    /// <param name="value">The stable version text.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="value"/> is blank.</exception>
    public ChunkerVersion(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the stable version text.</summary>
    public string Value { get; }

    /// <summary>Returns the stable version text.</summary>
    /// <returns>The exact text, or <see cref="string.Empty"/> for a default instance.</returns>
    public override string ToString() => Value ?? string.Empty;
}
