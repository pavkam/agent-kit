// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Session creation did not succeed.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its fields, safe to share across threads
/// without synchronization.
/// </remarks>
public sealed record AgentSessionCreationFailed: AgentSessionCreationResult
{
    /// <summary>Initializes a new instance of the <see cref="AgentSessionCreationFailed"/> record.</summary>
    /// <param name="agentId">The agent creation was requested for.</param>
    /// <param name="failure">The safe, closed evidence describing why creation did not succeed.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="agentId"/> is default.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="failure"/> is <see langword="null"/>.</exception>
    public AgentSessionCreationFailed(AgentId agentId, SessionCreationFailure failure)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, default, nameof(agentId));
        ArgumentNullException.ThrowIfNull(failure);

        AgentId = agentId;
        Failure = failure;
    }

    /// <summary>Gets the agent creation was requested for.</summary>
    public AgentId AgentId { get; init; }

    /// <summary>Gets the safe, closed evidence describing why creation did not succeed.</summary>
    public SessionCreationFailure Failure { get; init; }
}
