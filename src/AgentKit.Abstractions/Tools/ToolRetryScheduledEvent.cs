// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that a failed attempt will be retried after a bounded delay.</summary>
public sealed record ToolRetryScheduledEvent: ToolEvent
{
    /// <summary>Initializes a retry-scheduled event.</summary>
    /// <param name="agentId">The nondefault owning agent.</param><param name="sessionId">The nondefault owning session.</param>
    /// <param name="runId">The nondefault active run.</param><param name="turnId">The nondefault active turn.</param>
    /// <param name="operationId">The nondefault causal operation.</param><param name="callId">The nondefault call identity.</param>
    /// <param name="occurredAt">When the retry decision was made.</param>
    /// <param name="toolId">The resolved canonical tool identity.</param><param name="toolVersion">The resolved exact version.</param>
    /// <param name="failedAttempt">The positive attempt that just failed, counting the first attempt as one.</param>
    /// <param name="delay">The nonnegative backoff delay before the next attempt starts.</param>
    /// <exception cref="ArgumentOutOfRangeException">An identity is default, <paramref name="failedAttempt"/> is not positive, or <paramref name="delay"/> is negative.</exception>
    public ToolRetryScheduledEvent(
        AgentId agentId, SessionId sessionId, RunId runId, TurnId turnId, OperationId operationId, ToolCallId callId,
        DateTimeOffset occurredAt, ToolId toolId, ToolVersion toolVersion, int failedAttempt, TimeSpan delay)
        : base(agentId, sessionId, runId, turnId, operationId, callId, occurredAt)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(toolId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(toolVersion, default);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(failedAttempt);
        ArgumentOutOfRangeException.ThrowIfLessThan(delay, TimeSpan.Zero);
        ToolId = toolId;
        ToolVersion = toolVersion;
        FailedAttempt = failedAttempt;
        Delay = delay;
    }

    /// <summary>Gets the resolved canonical tool.</summary>
    public ToolId ToolId { get; }

    /// <summary>Gets the resolved exact tool version.</summary>
    public ToolVersion ToolVersion { get; }

    /// <summary>Gets the attempt that failed.</summary>
    /// <value>A positive count where one is the first attempt.</value>
    public int FailedAttempt { get; }

    /// <summary>Gets the backoff delay before the next attempt.</summary>
    /// <value>A nonnegative duration computed from the plan's retry policy.</value>
    public TimeSpan Delay { get; }
}
