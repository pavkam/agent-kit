// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Scopes durable memory and retrieval within one agent's logical partition.</summary>
/// <remarks>Values are preserved exactly without trimming and compare with ordinal, case-sensitive semantics.</remarks>
public readonly record struct MemoryNamespace
{
    /// <summary>Initializes a validated memory namespace.</summary>
    /// <param name="value">The non-blank namespace text.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is blank.</exception>
    public MemoryNamespace(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the namespace text.</summary>
    public string Value { get; }

    /// <inheritdoc/>
    public override string ToString() => Value;
}
