// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals;

/// <summary>Validates a settled child's reported result before a parent may rely on it.</summary>
/// <remarks>Child output is untrusted agent-produced data. A completed child is eligible only when its outcome reports success, its summary is bounded, and its evidence is present when the acceptance criteria require it. Validation checks shape and presence; verifying that evidence actually supports the claim remains the parent's responsibility.</remarks>
internal static class GoalResultValidation
{
    /// <summary>Validates one settled child.</summary>
    /// <param name="record">The child's aggregate.</param>
    /// <param name="criteria">The acceptance criteria the delegation stated, or <see langword="null"/> when they are not available.</param>
    /// <param name="maximumSummaryCharacters">The largest accepted summary.</param>
    /// <returns>Whether the result is eligible, and a bounded reason code when it is not.</returns>
    internal static (bool Eligible, string? Reason) Validate(GoalRecord record, AcceptanceCriteria? criteria, int maximumSummaryCharacters)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumSummaryCharacters);
        if (record.Goal.Status != GoalStatus.Completed)
        {
            return (false, "not_completed");
        }

        var outcome = record.Attempts.LastOrDefault()?.Outcome;
        return outcome is null || outcome.Status != DelegationStatus.Succeeded
            ? (false, "no_successful_outcome")
            : outcome.Result is null
                ? (false, "result_missing")
                : outcome.Result.Summary.Length > maximumSummaryCharacters
                    ? (false, "summary_too_long")
                    : criteria is { RequiresEvidence: true } && outcome.Evidence.IsEmpty
                        ? (false, "evidence_required")
                        : (true, null);
    }
}
