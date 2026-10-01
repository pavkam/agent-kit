// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Tests;

/// <summary>Supplies the goal-transition codec and a deterministic entry to the shared codec suite.</summary>
public sealed class GoalTransitionSessionEntryCodecFixture: ISessionEntryCodecConformanceFixture
{
    /// <inheritdoc/>
    public ISessionEntryCodec CreateCodec() => new GoalTransitionSessionEntryCodec();

    /// <inheritdoc/>
    public SessionEntry CreateEntry() => GoalSessionEntryTestData.Transitioned();

    /// <inheritdoc/>
    public bool SemanticallyEquivalent(SessionEntry expected, SessionEntry actual) =>
        expected is GoalTransitionSessionEntry left
        && actual is GoalTransitionSessionEntry right
        && left.Id == right.Id
        && left.Address == right.Address
        && left.Correlation == right.Correlation
        && left.BranchId == right.BranchId
        && left.Sequence == right.Sequence
        && left.CausalParentId == right.CausalParentId
        && left.RecordedAt == right.RecordedAt
        && left.Transition == right.Transition
        && left.Attempt is GoalAttemptStart { Attempt: var leftAttempt }
        && right.Attempt is GoalAttemptStart { Attempt: var rightAttempt }
        && leftAttempt == rightAttempt
        && left.SettledSequence == right.SettledSequence;
}
