// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The agent exists and resolved to a runnable handle.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its fields, safe to share across threads
/// without synchronization.
/// </remarks>
public sealed record ResolvedAgent: AgentResolution
{
    private readonly Agent _agent;

    /// <summary>Initializes a new instance of the <see cref="ResolvedAgent"/> record.</summary>
    /// <param name="agent">The resolved, engine-bound handle.</param>
    /// <exception cref="ArgumentNullException"><paramref name="agent"/> is <see langword="null"/>.</exception>
    public ResolvedAgent(Agent agent)
    {
        ArgumentNullException.ThrowIfNull(agent);
        _agent = agent;
    }

    /// <summary>Gets the resolved, engine-bound handle.</summary>
    /// <exception cref="ArgumentNullException">An initializer attempts to set <see langword="null"/>.</exception>
    public Agent Agent
    {
        get => _agent;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Agent));
            _agent = value;
        }
    }
}
