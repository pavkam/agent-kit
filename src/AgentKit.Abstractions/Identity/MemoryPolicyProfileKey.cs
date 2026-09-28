// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Selects one named memory retention and proposal policy profile.</summary>
/// <remarks>Values are preserved exactly without trimming and compare with ordinal, case-sensitive semantics.</remarks>
public readonly record struct MemoryPolicyProfileKey
{
    /// <summary>Initializes a validated memory-policy profile key.</summary>
    /// <param name="value">The non-blank canonical profile key.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="value"/> is empty or whitespace.</exception>
    public MemoryPolicyProfileKey(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the canonical profile key text.</summary>
    public string Value { get; }

    /// <inheritdoc/>
    public override string ToString() => Value ?? string.Empty;
}
