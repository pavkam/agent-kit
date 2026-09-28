// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Selects one keyed vector-index registration bound to a compatible embedding space.</summary>
/// <remarks>Values are preserved exactly without trimming and compare with ordinal, case-sensitive semantics.</remarks>
public readonly record struct VectorIndexKey
{
    /// <summary>Initializes a validated vector-index selection key.</summary>
    /// <param name="value">The non-blank canonical index key.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="value"/> is empty or whitespace.</exception>
    public VectorIndexKey(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the canonical index key text.</summary>
    public string Value { get; }

    /// <inheritdoc/>
    public override string ToString() => Value ?? string.Empty;
}
