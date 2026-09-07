// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The version of one registered <see cref="OutputDefinition"/>.</summary>
/// <remarks>
/// This type is an immutable value object with structural (ordinal,
/// textual) equality over <see cref="Value"/>. It carries no mutable state
/// itself and is safe to share and compare across threads without
/// synchronization.
/// </remarks>
public readonly record struct OutputDefinitionVersion
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OutputDefinitionVersion"/>
    /// struct, validating that it carries usable version text.
    /// </summary>
    /// <param name="value">The non-empty canonical version text.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    public OutputDefinitionVersion(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the canonical version text.</summary>
    public string Value { get; }

    /// <summary>
    /// Returns the canonical version text, suitable for logging and
    /// diagnostic messages.
    /// </summary>
    public override string ToString() => Value;
}
