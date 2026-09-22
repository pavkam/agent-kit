// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The explicit non-secret environment projection for one process start.</summary>
public sealed record EnvironmentProjection
{
    /// <summary>Initializes an environment projection.</summary>
    /// <param name="variables">The ordered environment variables.</param>
    /// <exception cref="ArgumentException">The array is default or contains null entries.</exception>
    public EnvironmentProjection(ImmutableArray<ProcessEnvironmentVariable> variables)
    {
        ArgumentException.ThrowIfDefault(variables);
        ArgumentException.ThrowIfContainsNull(variables);
        Variables = variables;
    }

    /// <summary>Gets the ordered environment variables.</summary>
    public ImmutableArray<ProcessEnvironmentVariable> Variables { get; init; }
}
