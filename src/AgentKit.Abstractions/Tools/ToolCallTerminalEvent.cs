// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that one call reached its authoritative terminal outcome.</summary>
/// <remarks>Published exactly once per identified call after the terminal record write was attempted; <see cref="Recorded"/> states whether it became durable.</remarks>
public sealed record ToolCallTerminalEvent: ToolEvent
{
    /// <summary>Initializes a terminal-outcome event.</summary>
    /// <param name="agentId">The nondefault owning agent.</param><param name="sessionId">The nondefault owning session.</param>
    /// <param name="runId">The nondefault active run.</param><param name="turnId">The nondefault active turn.</param>
    /// <param name="operationId">The nondefault causal operation.</param><param name="callId">The nondefault call identity.</param>
    /// <param name="occurredAt">When the terminal outcome was settled.</param>
    /// <param name="toolId">The resolved canonical tool, or null for an unresolved alias.</param>
    /// <param name="toolVersion">The resolved exact version; present exactly when <paramref name="toolId"/> is.</param>
    /// <param name="status">The authoritative terminal status.</param>
    /// <param name="sideEffectCertainty">The defined side-effect certainty of the outcome.</param>
    /// <param name="retryable">Whether the runtime advises that the call may be retried.</param>
    /// <param name="accepted">Whether an accepted record preceded invocation.</param>
    /// <param name="recorded">Whether the terminal record became durable.</param>
    /// <exception cref="ArgumentOutOfRangeException">An identity is default or <paramref name="sideEffectCertainty"/> is undefined.</exception>
    /// <exception cref="ArgumentException"><paramref name="toolId"/> and <paramref name="toolVersion"/> are not both present or both absent.</exception>
    public ToolCallTerminalEvent(
        AgentId agentId, SessionId sessionId, RunId runId, TurnId turnId, OperationId operationId, ToolCallId callId,
        DateTimeOffset occurredAt, ToolId? toolId, ToolVersion? toolVersion, ToolTerminalStatus status,
        SideEffectCertainty sideEffectCertainty, bool retryable, bool accepted, bool recorded)
        : base(agentId, sessionId, runId, turnId, operationId, callId, occurredAt)
    {
        ArgumentException.ThrowIfNotEqual(toolId.HasValue, toolVersion.HasValue, nameof(toolVersion));
        if (toolId is { } id)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(id, default, nameof(toolId));
            ArgumentOutOfRangeException.ThrowIfEqual(toolVersion!.Value, default, nameof(toolVersion));
        }

        ArgumentOutOfRangeException.ThrowIfUndefined(sideEffectCertainty);
        ToolId = toolId;
        ToolVersion = toolVersion;
        Status = status;
        SideEffectCertainty = sideEffectCertainty;
        Retryable = retryable;
        Accepted = accepted;
        Recorded = recorded;
    }

    /// <summary>Gets the resolved canonical tool.</summary>
    /// <value>Null when the requested alias never resolved.</value>
    public ToolId? ToolId { get; }

    /// <summary>Gets the resolved exact tool version.</summary>
    /// <value>Present exactly when <see cref="ToolId"/> is.</value>
    public ToolVersion? ToolVersion { get; }

    /// <summary>Gets the authoritative terminal status.</summary>
    public ToolTerminalStatus Status { get; }

    /// <summary>Gets the defined side-effect certainty.</summary>
    public SideEffectCertainty SideEffectCertainty { get; }

    /// <summary>Gets whether the runtime advises a retry of the call.</summary>
    public bool Retryable { get; }

    /// <summary>Gets whether an accepted-call record preceded invocation.</summary>
    /// <value><see langword="false"/> for every pre-acceptance rejection.</value>
    public bool Accepted { get; }

    /// <summary>Gets whether the terminal record became durable.</summary>
    /// <value><see langword="false"/> when the recorder rejected or failed; the outcome itself is unchanged.</value>
    public bool Recorded { get; }
}
