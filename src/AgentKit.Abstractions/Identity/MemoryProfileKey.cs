// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Selects one named durable-memory storage and retrieval profile.</summary>
/// <remarks>This immutable value uses ordinal text equality and identifies configuration only; it neither contains retrieved content nor grants store access. Values are preserved exactly without trimming or normalization and compare with ordinal, case-sensitive semantics. A default instance has no usable selection text; consumers must reject it at their boundary.</remarks>
public readonly record struct MemoryProfileKey
{
    /// <summary>Initializes a validated memory-profile selection key.</summary>
    /// <param name="value">The non-blank canonical profile key.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="value"/> is empty or consists only of whitespace.</exception>
    public MemoryProfileKey(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the canonical profile key text.</summary>
    /// <value>The exact caller-supplied text. A default instance exposes <see langword="null"/> at runtime.</value>
    public string Value { get; }

    /// <summary>Returns the canonical profile key text for diagnostics and configuration.</summary>
    /// <returns>The exact key text, or <see cref="string.Empty"/> for a default instance.</returns>
    public override string ToString() => Value ?? string.Empty;
}
