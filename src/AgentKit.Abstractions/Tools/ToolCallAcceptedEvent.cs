// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that a call was authorized and its accepted record is durable, immediately before invocation.</summary>
public sealed record ToolCallAcceptedEvent: ToolEvent
{
    /// <summary>Initializes an accepted-call event.</summary>
    /// <param name="agentId">The nondefault owning agent.</param><param name="sessionId">The nondefault owning session.</param>
    /// <param name="runId">The nondefault active run.</param><param name="turnId">The nondefault active turn.</param>
    /// <param name="operationId">The nondefault causal operation.</param><param name="callId">The nondefault call identity.</param>
    /// <param name="occurredAt">When the accepted record was committed.</param>
    /// <param name="toolId">The resolved canonical tool identity.</param><param name="toolVersion">The resolved exact version.</param>
    /// <param name="executionPolicy">The exact execution policy that planned the call.</param>
    /// <exception cref="ArgumentOutOfRangeException">An identity, <paramref name="toolId"/>, or <paramref name="toolVersion"/> is default.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="executionPolicy"/> is null.</exception>
    public ToolCallAcceptedEvent(
        AgentId agentId, SessionId sessionId, RunId runId, TurnId turnId, OperationId operationId, ToolCallId callId,
        DateTimeOffset occurredAt, ToolId toolId, ToolVersion toolVersion, ToolExecutionPolicyReference executionPolicy)
        : base(agentId, sessionId, runId, turnId, operationId, callId, occurredAt)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(toolId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(toolVersion, default);
        ArgumentNullException.ThrowIfNull(executionPolicy);
        ToolId = toolId;
        ToolVersion = toolVersion;
        ExecutionPolicy = executionPolicy;
    }

    /// <summary>Gets the resolved canonical tool.</summary>
    public ToolId ToolId { get; }

    /// <summary>Gets the resolved exact tool version.</summary>
    public ToolVersion ToolVersion { get; }

    /// <summary>Gets the execution policy that planned the call.</summary>
    public ToolExecutionPolicyReference ExecutionPolicy { get; }
}
