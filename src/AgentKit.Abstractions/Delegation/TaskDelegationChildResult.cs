// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports one durably created child goal after its current attempt has terminally settled.</summary>
public sealed record TaskDelegationChildResult: TaskDelegationResult
{
    /// <summary>Initializes a terminal child result.</summary>
    /// <param name="id">The delegation identity.</param>
    /// <param name="childGoalId">The durable child goal.</param>
    /// <param name="childAgentId">The selected child agent.</param>
    /// <param name="childSessionId">The isolated child session.</param>
    /// <param name="childAttemptId">The child attempt, when one started.</param>
    /// <param name="childRunId">The child run, when one started.</param>
    /// <param name="status">The terminal child status.</param>
    /// <param name="summary">The bounded untrusted child summary.</param>
    /// <param name="sideEffectCertainty">What is known about child effects.</param>
    /// <exception cref="ArgumentException"><paramref name="summary"/> is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="status"/> is undefined or an identity is empty.</exception>
    public TaskDelegationChildResult(
        DelegationId id,
        GoalId childGoalId,
        AgentId childAgentId,
        SessionId childSessionId,
        GoalAttemptId? childAttemptId,
        RunId? childRunId,
        TaskDelegationStatus status,
        string summary,
        SideEffectCertainty sideEffectCertainty) : base(id)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(childGoalId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(childAgentId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(childSessionId, default);
        ArgumentOutOfRangeException.ThrowIfUndefined(status);
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);
        ArgumentOutOfRangeException.ThrowIfUndefined(sideEffectCertainty);
        ChildGoalId = childGoalId;
        ChildAgentId = childAgentId;
        ChildSessionId = childSessionId;
        ChildAttemptId = childAttemptId;
        ChildRunId = childRunId;
        Status = status;
        Summary = summary;
        SideEffectCertainty = sideEffectCertainty;
    }

    /// <summary>Gets the durable child goal.</summary>
    public GoalId ChildGoalId { get; init; }
    /// <summary>Gets the selected child agent.</summary>
    public AgentId ChildAgentId { get; init; }
    /// <summary>Gets the isolated child session.</summary>
    public SessionId ChildSessionId { get; init; }
    /// <summary>Gets the child attempt, when one started.</summary>
    public GoalAttemptId? ChildAttemptId { get; init; }
    /// <summary>Gets the child run, when one started.</summary>
    public RunId? ChildRunId { get; init; }
    /// <summary>Gets the terminal child status.</summary>
    public TaskDelegationStatus Status { get; init; }
    /// <summary>Gets the bounded, untrusted child summary.</summary>
    public string Summary { get; init; }
    /// <summary>Gets what is known about child side effects.</summary>
    public SideEffectCertainty SideEffectCertainty { get; init; }
}
