// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies one additively registered <see cref="IOutputValidator"/> an output definition selects by name.</summary>
/// <remarks>
/// This type is an immutable value object with structural (ordinal,
/// textual) equality over <see cref="Name"/>. It carries no mutable state
/// itself and is safe to share, compare, and use as a lookup key across
/// threads without synchronization.
/// </remarks>
public readonly record struct OutputValidatorReference
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OutputValidatorReference"/>
    /// struct, validating that it carries usable name text.
    /// </summary>
    /// <param name="name">The non-empty name of the referenced validator.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    public OutputValidatorReference(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    /// <summary>Gets the name of the referenced validator.</summary>
    public string Name { get; }

    /// <summary>
    /// Returns the referenced validator's name, suitable for logging and
    /// diagnostic messages.
    /// </summary>
    public override string ToString() => Name;
}
