// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Tests;

/// <summary>Supplies the goal-created codec and a deterministic entry to the shared codec suite.</summary>
public sealed class GoalCreatedSessionEntryCodecFixture: ISessionEntryCodecConformanceFixture
{
    /// <inheritdoc/>
    public ISessionEntryCodec CreateCodec() => new GoalCreatedSessionEntryCodec();

    /// <inheritdoc/>
    public SessionEntry CreateEntry() => GoalSessionEntryTestData.Created();

    /// <inheritdoc/>
    public bool SemanticallyEquivalent(SessionEntry expected, SessionEntry actual) =>
        expected is GoalCreatedSessionEntry left
        && actual is GoalCreatedSessionEntry right
        && left.Id == right.Id
        && left.Address == right.Address
        && left.Correlation == right.Correlation
        && left.BranchId == right.BranchId
        && left.Sequence == right.Sequence
        && left.CausalParentId == right.CausalParentId
        && left.RecordedAt == right.RecordedAt
        && left.CreateKey == right.CreateKey
        && left.Record == right.Record;
}
