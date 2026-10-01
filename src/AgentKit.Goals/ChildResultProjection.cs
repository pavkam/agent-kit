// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals;

/// <summary>Projects a durable child aggregate into the result a parent receives.</summary>
/// <remarks>The projection is derived entirely from durable state, so a parent that waits, a parent that retries, and a parent that recovers after process loss all observe the same result. A completed child whose result fails validation is reported as failed with its result withheld, because a child result cannot complete its parent without validation.</remarks>
internal static class ChildResultProjection
{
    private const string _validationKey = "agentkit.goals.validation";

    /// <summary>Builds the result for one child at its current durable state.</summary>
    /// <param name="delegationId">The delegation identity the caller knows.</param>
    /// <param name="record">The child's aggregate.</param>
    /// <param name="criteria">The acceptance criteria, or <see langword="null"/> when unavailable.</param>
    /// <param name="maximumSummaryCharacters">The largest accepted summary.</param>
    /// <returns>A child result that is terminal or <see cref="DelegationStatus.Dispatched"/>.</returns>
    internal static DelegationChildResult Project(DelegationId delegationId, GoalRecord record, AcceptanceCriteria? criteria, int maximumSummaryCharacters)
    {
        ArgumentNullException.ThrowIfNull(record);
        var goal = record.Goal;
        var attempt = record.Attempts.LastOrDefault();
        var outcome = attempt?.Outcome;
        var agent = attempt?.AgentId ?? record.Delegation?.TargetAgentId ?? goal.OwnerAgentId;
        var (eligible, reason) = GoalResultValidation.Validate(record, criteria, maximumSummaryCharacters);
        var status = goal.Status switch
        {
            GoalStatus.Completed => eligible ? DelegationStatus.Succeeded : DelegationStatus.Failed,
            GoalStatus.Failed => DelegationStatus.Failed,
            GoalStatus.Cancelled => DelegationStatus.Cancelled,
            GoalStatus.Blocked => DelegationStatus.Blocked,
            GoalStatus.Proposed or GoalStatus.Ready or GoalStatus.Active or GoalStatus.Waiting => DelegationStatus.Dispatched,
            _ => DelegationStatus.Failed,
        };
        var extensions = goal.Status == GoalStatus.Completed && !eligible && reason is not null
            ? new ExtensionData(ImmutableDictionary<string, ExtensionValue>.Empty.Add(
                _validationKey, new ExtensionValue([.. System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(reason)])))
            : ExtensionData.Empty;
        var certainty = outcome?.SideEffectCertainty
            ?? (attempt is null ? SideEffectCertainty.DefinitelyNotPerformed : SideEffectCertainty.Unknown);
        return new DelegationChildResult(
            delegationId,
            goal.Id,
            agent,
            attempt?.SessionId,
            attempt?.Id,
            outcome?.RunId,
            status,
            status == DelegationStatus.Succeeded ? outcome?.Result : null,
            outcome?.Evidence ?? [],
            outcome?.Usage ?? GoalBudgetUsage.None,
            certainty,
            extensions);
    }

    /// <summary>Determines whether a goal status can no longer change without outside action.</summary>
    /// <param name="status">The status to classify.</param>
    /// <returns><see langword="true"/> for completed, failed, cancelled, and blocked.</returns>
    internal static bool IsSettled(GoalStatus status) =>
        status is GoalStatus.Completed or GoalStatus.Failed or GoalStatus.Cancelled or GoalStatus.Blocked;
}
