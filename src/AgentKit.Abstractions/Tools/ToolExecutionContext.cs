// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The immutable operation context threaded through one tool invocation
/// attempt: which call, on whose behalf, and within which causal operation.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization. <see cref="ToolCallId"/> is the same identity
/// that flows through the model-facing <see cref="ToolCallPart"/> and its
/// terminal <see cref="ToolResultPart"/>, so a tool invocation is always
/// traceable back to the exact call that requested it.
/// </remarks>
public sealed record ToolExecutionContext
{
    /// <summary>Initializes a new instance of the <see cref="ToolExecutionContext"/> record.</summary>
    /// <param name="agentId">The agent this invocation occurred for.</param>
    /// <param name="sessionId">The session this invocation occurred within, when applicable.</param>
    /// <param name="toolCallId">The call this invocation answers.</param>
    /// <param name="correlation">The causal operation performing this invocation.</param>
    /// <param name="identity">The identity on whose behalf this invocation is performed.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="correlation"/> or <paramref name="identity"/> is null.
    /// </exception>
    public ToolExecutionContext(
        AgentId agentId,
        SessionId? sessionId,
        ToolCallId toolCallId,
        OperationCorrelation correlation,
        ExecutionIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(correlation);
        ArgumentNullException.ThrowIfNull(identity);

        AgentId = agentId;
        SessionId = sessionId;
        ToolCallId = toolCallId;
        Correlation = correlation;
        Identity = identity;
    }

    /// <summary>Gets the agent this invocation occurred for.</summary>
    public AgentId AgentId { get; init; }

    /// <summary>Gets the session this invocation occurred within, when applicable.</summary>
    public SessionId? SessionId { get; init; }

    /// <summary>Gets the call this invocation answers.</summary>
    public ToolCallId ToolCallId { get; init; }

    /// <summary>Gets the causal operation performing this invocation.</summary>
    public OperationCorrelation Correlation { get; init; }

    /// <summary>Gets the identity on whose behalf this invocation is performed.</summary>
    public ExecutionIdentity Identity { get; init; }
}
