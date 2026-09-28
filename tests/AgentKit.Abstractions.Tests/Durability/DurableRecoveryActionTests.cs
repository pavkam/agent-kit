// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies <see cref="DurableRecoveryAction"/> covers every decision kind exactly once.</summary>
public sealed class DurableRecoveryActionTests
{
    [Fact]
    public void Members_WhenCounted_MatchTheClosedRecoveryDecisionHierarchy()
    {
        var decisionKinds = typeof(RecoveryDecision).Assembly
            .GetTypes()
            .Count(static type => type.IsSubclassOf(typeof(RecoveryDecision)));

        Enum.GetValues<DurableRecoveryAction>().Length.ShouldBe(decisionKinds);
    }

    [Fact]
    public void Members_WhenInspected_AreNamedForTheirDecision()
    {
        Enum.GetNames<DurableRecoveryAction>().ShouldBe([
            nameof(DurableRecoveryAction.Start),
            nameof(DurableRecoveryAction.Reconcile),
            nameof(DurableRecoveryAction.Retry),
            nameof(DurableRecoveryAction.CommitRecordedResult),
            nameof(DurableRecoveryAction.RequireOperator),
            nameof(DurableRecoveryAction.NotPossible),
        ]);
    }
}
