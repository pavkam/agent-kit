// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Records the verified-by-reference outcome of one attempt.</summary>
/// <remarks>A completed goal cites an outcome reference rather than trusting an assistant claim. The result and evidence remain untrusted agent-produced data until the owner validates them; this record preserves them with the run that produced them and the truthful side-effect certainty.</remarks>
public sealed record GoalOutcomeReference
{
    /// <summary>Initializes a validated outcome reference.</summary>
    /// <param name="runId">The run that produced the outcome.</param>
    /// <param name="status">The terminal status; <see cref="DelegationStatus.Dispatched"/> is not terminal and is rejected.</param>
    /// <param name="result">The reported result, or <see langword="null"/> when none was produced.</param>
    /// <param name="evidence">The evidence references; empty when none.</param>
    /// <param name="usage">The consumed usage.</param>
    /// <param name="sideEffectCertainty">The truthful effect certainty.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="runId"/> is default, <paramref name="status"/> is undefined or not terminal, or <paramref name="sideEffectCertainty"/> is undefined.</exception>
    /// <exception cref="ArgumentException"><paramref name="evidence"/> is default.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="usage"/> or an evidence entry is null.</exception>
    public GoalOutcomeReference(
        RunId runId,
        DelegationStatus status,
        StructuredGoalResult? result,
        ImmutableArray<EvidenceReference> evidence,
        GoalBudgetUsage usage,
        SideEffectCertainty sideEffectCertainty)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(runId, default);
        ArgumentOutOfRangeException.ThrowIfUndefined(status);
        ArgumentOutOfRangeException.ThrowIfEqual(status, DelegationStatus.Dispatched);
        ArgumentException.ThrowIfDefault(evidence);
        ArgumentException.ThrowIfContainsNull(evidence);
        ArgumentNullException.ThrowIfNull(usage);
        ArgumentOutOfRangeException.ThrowIfUndefined(sideEffectCertainty);
        RunId = runId;
        Status = status;
        Result = result;
        Evidence = evidence;
        Usage = usage;
        SideEffectCertainty = sideEffectCertainty;
    }

    /// <summary>Gets the run that produced the outcome.</summary>
    public RunId RunId { get; }

    /// <summary>Gets the terminal status.</summary>
    public DelegationStatus Status { get; }

    /// <summary>Gets the reported result, or <see langword="null"/>.</summary>
    public StructuredGoalResult? Result { get; }

    /// <summary>Gets the evidence references.</summary>
    public ImmutableArray<EvidenceReference> Evidence { get; }

    /// <summary>Gets the consumed usage.</summary>
    public GoalBudgetUsage Usage { get; }

    /// <summary>Gets the truthful side-effect certainty.</summary>
    public SideEffectCertainty SideEffectCertainty { get; }

    /// <inheritdoc/>
    public bool Equals(GoalOutcomeReference? other) =>
        other is not null
        && RunId == other.RunId
        && Status == other.Status
        && Result == other.Result
        && Evidence.SequenceEqual(other.Evidence)
        && Usage == other.Usage
        && SideEffectCertainty == other.SideEffectCertainty;

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(RunId);
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
