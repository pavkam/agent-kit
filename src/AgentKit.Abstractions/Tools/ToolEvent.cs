// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Defines the closed family of immutable, content-free tool-runtime events delivered to <see cref="IToolEventSink"/>.</summary>
/// <remarks>
/// Every event names the exact agent, session, run, turn, operation, and call it concerns. The requested alias, arguments,
/// results, and exception text are deliberately absent: they are untrusted or protected content that the authoritative
/// <see cref="ToolCallResult"/> and the durable records already retain under their own controls.
/// </remarks>
public abstract record ToolEvent
{
    /// <summary>Initializes the identity every tool event carries.</summary>
    /// <param name="agentId">The nondefault owning agent.</param>
    /// <param name="sessionId">The nondefault owning session.</param>
    /// <param name="runId">The nondefault active run.</param>
    /// <param name="turnId">The nondefault active turn.</param>
    /// <param name="operationId">The nondefault causal operation.</param>
    /// <param name="callId">The nondefault provider-correlated call identity.</param>
    /// <param name="occurredAt">The instant the runtime established the fact, from the injected clock.</param>
    /// <exception cref="ArgumentOutOfRangeException">An identity is default.</exception>
    /// <exception cref="ArgumentException">The constructed runtime type is outside the closed event family.</exception>
    private protected ToolEvent(
        AgentId agentId,
        SessionId sessionId,
        RunId runId,
        TurnId turnId,
        OperationId operationId,
        ToolCallId callId,
        DateTimeOffset occurredAt)
    {
        AcceptedToolCall.ValidateIdentities(agentId, sessionId, runId, turnId, operationId, callId);
        ArgumentException.ThrowIfNotEqual(
            this is ToolCallAcceptedEvent or ToolRetryScheduledEvent or ToolCallTerminalEvent,
            true,
            "event");
        AgentId = agentId;
        SessionId = sessionId;
        RunId = runId;
        TurnId = turnId;
        OperationId = operationId;
        CallId = callId;
        OccurredAt = occurredAt;
    }

    /// <summary>Copies the base state of a supported immutable event.</summary>
    /// <param name="original">The nonnull original event.</param>
    /// <exception cref="ArgumentNullException"><paramref name="original"/> is null.</exception>
    protected ToolEvent(ToolEvent original)
    {
        ArgumentNullException.ThrowIfNull(original);
        AgentId = original.AgentId;
        SessionId = original.SessionId;
        RunId = original.RunId;
        TurnId = original.TurnId;
        OperationId = original.OperationId;
        CallId = original.CallId;
        OccurredAt = original.OccurredAt;
    }

    /// <summary>Gets the owning agent.</summary>
    public AgentId AgentId { get; }

    /// <summary>Gets the owning session.</summary>
    public SessionId SessionId { get; }

    /// <summary>Gets the active run.</summary>
    public RunId RunId { get; }

    /// <summary>Gets the active turn.</summary>
    public TurnId TurnId { get; }

    /// <summary>Gets the causal operation.</summary>
    public OperationId OperationId { get; }

    /// <summary>Gets the provider-correlated call identity.</summary>
    public ToolCallId CallId { get; }

    /// <summary>Gets when the runtime established the fact.</summary>
    /// <value>A timestamp from the injected <see cref="TimeProvider"/> with no ordering inference against other events.</value>
    public DateTimeOffset OccurredAt { get; }
}
