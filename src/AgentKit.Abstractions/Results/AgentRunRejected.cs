// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports rejection before run acceptance without inventing a run identity or terminal lifecycle.</summary>
/// <typeparam name="TOutput">The requested validated output type; rejection contains no output value.</typeparam>
/// <remarks>The mapper supplies a safe error. Unauthorized rejection must not disclose whether the requested session exists.</remarks>
public sealed record AgentRunRejected<TOutput>: AgentRunResult<TOutput>
{
    /// <summary>Captures the requested address and normalized admission failure.</summary>
    /// <param name="agentId">The nondefault requested agent identity.</param>
    /// <param name="sessionId">The nondefault requested session identity.</param>
    /// <param name="failure">The nonnull safe normalized admission error.</param>
    /// <exception cref="ArgumentOutOfRangeException">An identity is default.</exception>
    /// <exception cref="ArgumentNullException">The failure is null.</exception>
    public AgentRunRejected(AgentId agentId, SessionId sessionId, AgentError failure)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(sessionId, default);
        ArgumentNullException.ThrowIfNull(failure);
        AgentId = agentId; SessionId = sessionId; Failure = failure;
    }
    /// <summary>Gets the agent requested at admission.</summary>
    /// <value>A nondefault requested address, not proof that the agent was admitted.</value>
    public AgentId AgentId { get; }
    /// <summary>Gets the requested session address without attesting its existence.</summary>
    /// <value>A nondefault requested session identity.</value>
    public SessionId SessionId { get; }
    /// <summary>Gets the normalized admission failure.</summary>
    /// <value>A nonnull safe error whose mapping avoids unauthorized existence disclosure.</value>
    public AgentError Failure { get; }
}
