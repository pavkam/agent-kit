// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The requested definition was resolved.</summary>
public sealed record OutputDefinitionResolved: OutputDefinitionResult
{
    /// <summary>Initializes a new instance of the <see cref="OutputDefinitionResolved"/> record.</summary>
    /// <param name="definition">The resolved definition.</param>
    /// <exception cref="ArgumentNullException"><paramref name="definition"/> is null.</exception>
    public OutputDefinitionResolved(OutputDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        Definition = definition;
    }

    /// <summary>Gets the resolved definition.</summary>
    public OutputDefinition Definition { get; init; }
}
