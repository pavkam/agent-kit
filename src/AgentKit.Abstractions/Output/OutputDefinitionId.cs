// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies one registered <see cref="OutputDefinition"/>, independent of its version.</summary>
/// <remarks>
/// This type is an immutable value object with structural (ordinal,
/// textual) equality over <see cref="Value"/>. It carries no mutable state
/// itself and is safe to share, compare, and use as a lookup key across
/// threads without synchronization.
/// </remarks>
public readonly record struct OutputDefinitionId
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OutputDefinitionId"/>
    /// struct, validating that it carries usable identifier text.
    /// </summary>
    /// <param name="value">The non-empty canonical definition identifier text.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    public OutputDefinitionId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the canonical definition identifier text.</summary>
    public string Value { get; }

    /// <summary>
    /// Returns the canonical identifier text, suitable for logging and
    /// diagnostic messages.
    /// </summary>
    public override string ToString() => Value;
}
