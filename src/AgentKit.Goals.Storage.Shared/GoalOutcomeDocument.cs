// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Storage;

/// <summary>Is the persisted form of a <see cref="GoalOutcomeReference"/>.</summary>
/// <param name="RunId">The run that produced the outcome.</param>
/// <param name="Status">The terminal status.</param>
/// <param name="Result">The reported result, or <see langword="null"/>.</param>
/// <param name="Evidence">The evidence references.</param>
/// <param name="Usage">The consumed usage.</param>
/// <param name="SideEffectCertainty">The truthful effect certainty.</param>
internal sealed record GoalOutcomeDocument(
    Guid RunId,
    DelegationStatus Status,
    GoalResultDocument? Result,
    ImmutableArray<GoalEvidenceDocument> Evidence,
    GoalUsageDocument Usage,
    SideEffectCertainty SideEffectCertainty)
{
    /// <summary>Converts an outcome to its persisted form.</summary>
    /// <param name="value">The non-null outcome.</param>
    /// <returns>The document.</returns>
    internal static GoalOutcomeDocument FromDomain(GoalOutcomeReference value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(
            value.RunId.Value,
            value.Status,
            value.Result is null ? null : GoalResultDocument.FromDomain(value.Result),
            [.. value.Evidence.Select(GoalEvidenceDocument.FromDomain)],
            GoalUsageDocument.FromDomain(value.Usage),
            value.SideEffectCertainty);
    }

    /// <summary>Restores the outcome, re-running its validation.</summary>
    /// <returns>The outcome.</returns>
    internal GoalOutcomeReference ToDomain()
    {
        ArgumentNullException.ThrowIfNull(Usage);
        var evidence = Evidence.IsDefault ? [] : Evidence;
        ArgumentException.ThrowIfContainsNull(evidence, nameof(Evidence));
        return new(
            new RunId(RunId),
            Status,
            Result?.ToDomain(),
            [.. evidence.Select(static item => item.ToDomain())],
            Usage.ToDomain(),
            SideEffectCertainty);
    }
}
