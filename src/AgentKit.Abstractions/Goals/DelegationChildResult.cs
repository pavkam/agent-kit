// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports a delegated child that exists durably, either handed off or settled.</summary>
/// <remarks>
/// <para>
/// A local worker provisions the child session after handoff, so <see cref="ChildSessionId"/>, <see cref="ChildAttemptId"/>,
/// and <see cref="ChildRunId"/> are absent while <see cref="Status"/> is <see cref="DelegationStatus.Dispatched"/> and
/// present once those states exist. <see cref="Result"/> and <see cref="Evidence"/> are untrusted agent output until the
/// parent validates them; no status here can mutate the parent goal or session.
/// </para>
/// </remarks>
public sealed record DelegationChildResult: DelegationResult
{
    /// <summary>Initializes a child result.</summary>
    /// <param name="id">The delegation identity.</param>
    /// <param name="childGoalId">The durable child goal.</param>
    /// <param name="childAgentId">The agent that executes the child.</param>
    /// <param name="childSessionId">The child's session, or <see langword="null"/> until it is provisioned.</param>
    /// <param name="childAttemptId">The child's attempt, or <see langword="null"/> until one starts.</param>
    /// <param name="childRunId">The child's run, or <see langword="null"/> until one starts.</param>
    /// <param name="status">The defined status.</param>
    /// <param name="result">The reported result, or <see langword="null"/>.</param>
    /// <param name="evidence">The evidence references; empty when none.</param>
    /// <param name="usage">The consumed usage.</param>
    /// <param name="sideEffectCertainty">The truthful effect certainty.</param>
    /// <param name="extensions">The forward-compatible extension data.</param>
    /// <exception cref="ArgumentOutOfRangeException">An identity is default, or a status or certainty is undefined.</exception>
    /// <exception cref="ArgumentException"><paramref name="evidence"/> is default, or a dispatched child carries a result.</exception>
    /// <exception cref="ArgumentNullException">A required reference argument is null.</exception>
    public DelegationChildResult(
        DelegationId id,
        GoalId childGoalId,
        AgentId childAgentId,
        SessionId? childSessionId,
        GoalAttemptId? childAttemptId,
        RunId? childRunId,
        DelegationStatus status,
        StructuredGoalResult? result,
        ImmutableArray<EvidenceReference> evidence,
        GoalBudgetUsage usage,
        SideEffectCertainty sideEffectCertainty,
        ExtensionData extensions)
        : base(id, extensions)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(childGoalId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(childAgentId, default);
        if (childSessionId is { } session)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(session, default, nameof(childSessionId));
        }

        if (childAttemptId is { } attempt)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(attempt, default, nameof(childAttemptId));
        }

        if (childRunId is { } run)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(run, default, nameof(childRunId));
        }

        ArgumentOutOfRangeException.ThrowIfUndefined(status);
        ArgumentException.ThrowIfDefault(evidence);
        ArgumentException.ThrowIfContainsNull(evidence);
        ArgumentNullException.ThrowIfNull(usage);
        ArgumentOutOfRangeException.ThrowIfUndefined(sideEffectCertainty);
        ArgumentException.ThrowIfNotEqual(status is DelegationStatus.Dispatched && result is not null, false, nameof(result));
        ChildGoalId = childGoalId;
        ChildAgentId = childAgentId;
        ChildSessionId = childSessionId;
        ChildAttemptId = childAttemptId;
        ChildRunId = childRunId;
        Status = status;
        Result = result;
        Evidence = evidence;
        Usage = usage;
        SideEffectCertainty = sideEffectCertainty;
    }

    /// <summary>Gets the durable child goal.</summary>
    public GoalId ChildGoalId { get; }

    /// <summary>Gets the agent that executes the child.</summary>
    public AgentId ChildAgentId { get; }

    /// <summary>Gets the child's session, or <see langword="null"/> until it is provisioned.</summary>
    public SessionId? ChildSessionId { get; }

    /// <summary>Gets the child's attempt, or <see langword="null"/> until one starts.</summary>
    public GoalAttemptId? ChildAttemptId { get; }

    /// <summary>Gets the child's run, or <see langword="null"/> until one starts.</summary>
    public RunId? ChildRunId { get; }

    /// <summary>Gets the reported status.</summary>
    public DelegationStatus Status { get; }

    /// <summary>Gets the reported result, or <see langword="null"/>. It is untrusted until validated.</summary>
    public StructuredGoalResult? Result { get; }

    /// <summary>Gets the evidence references.</summary>
    public ImmutableArray<EvidenceReference> Evidence { get; }

    /// <summary>Gets the consumed usage.</summary>
    public GoalBudgetUsage Usage { get; }

    /// <summary>Gets the truthful side-effect certainty.</summary>
    public SideEffectCertainty SideEffectCertainty { get; }

    /// <inheritdoc/>
    public bool Equals(DelegationChildResult? other) =>
        other is not null
        && Id == other.Id
        && Extensions == other.Extensions
        && ChildGoalId == other.ChildGoalId
        && ChildAgentId == other.ChildAgentId
        && ChildSessionId == other.ChildSessionId
        && ChildAttemptId == other.ChildAttemptId
        && ChildRunId == other.ChildRunId
        && Status == other.Status
        && Result == other.Result
        && Evidence.SequenceEqual(other.Evidence)
        && Usage == other.Usage
        && SideEffectCertainty == other.SideEffectCertainty;

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Id);
        hash.Add(Extensions);
        hash.Add(ChildGoalId);
        hash.Add(ChildAgentId);
        hash.Add(ChildSessionId);
        hash.Add(ChildAttemptId);
        hash.Add(ChildRunId);
        hash.Add(Status);
        hash.Add(Result);
        foreach (var item in Evidence)
        {
            hash.Add(item);
        }

        hash.Add(Usage);
        hash.Add(SideEffectCertainty);
        return hash.ToHashCode();
    }
}
