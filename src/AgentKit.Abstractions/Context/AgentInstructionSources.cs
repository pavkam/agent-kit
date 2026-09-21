// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>A typed instruction-source bundle used to disambiguate agent-definition construction.</summary>
public readonly record struct AgentInstructionSources
{
    /// <summary>Initializes an instruction-source bundle.</summary>
    /// <param name="sources">The ordered instruction sources for one agent definition.</param>
    /// <exception cref="ArgumentException"><paramref name="sources"/> is a default array or contains null.</exception>
    public AgentInstructionSources(ImmutableArray<InstructionSource> sources)
    {
        ArgumentException.ThrowIfContainsNull(sources);
        Sources = sources;
    }

    /// <summary>Gets the ordered instruction sources for one agent definition.</summary>
    public ImmutableArray<InstructionSource> Sources { get; }
}
