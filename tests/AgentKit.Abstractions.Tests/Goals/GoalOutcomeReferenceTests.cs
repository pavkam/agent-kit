// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Goals;

using AgentKit.TestSupport;

/// <summary>Verifies GoalOutcomeReference constraints and value semantics.</summary>
public sealed class GoalOutcomeReferenceTests
{
    [Fact]
    public void Constructor_WhenStatusIsDispatched_ThrowsBecauseItIsNotTerminal() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Make(status: DelegationStatus.Dispatched)).ParamName.ShouldBe("status");

    [Fact]
    public void Constructor_WhenRunIsDefault_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Make(run: default(RunId))).ParamName.ShouldBe("runId");

    [Fact]
    public void Constructor_WhenEvidenceIsDefaultOrContainsNull_ThrowsArgumentException()
    {
        Should.Throw<ArgumentException>(() => Make(evidence: default(ImmutableArray<EvidenceReference>))).ParamName.ShouldBe("evidence");
        Should.Throw<ArgumentException>(() => Make(evidence: [null!])).ParamName.ShouldBe("evidence");
    }

    [Fact]
    public void Constructor_WhenCertaintyIsUndefined_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Make(certainty: (SideEffectCertainty) 99)).ParamName.ShouldBe("sideEffectCertainty");

    [Fact]
    public void Equality_WhenEvidenceMatchesByContent_IsStructural()
    {
        var run = GoalTestData.NewRun();
        var evidence = ImmutableArray.Create(new EvidenceReference("artifact", "a1"));

        Make(run: run, evidence: evidence).ShouldBe(Make(run: run, evidence: [new EvidenceReference("artifact", "a1")]));
    }

    private static GoalOutcomeReference Make(
        RunId? run = null,
        DelegationStatus status = DelegationStatus.Succeeded,
        ImmutableArray<EvidenceReference>? evidence = null,
        SideEffectCertainty certainty = SideEffectCertainty.Unknown) => new(
            run ?? GoalTestData.NewRun(), status, null, evidence ?? [], GoalBudgetUsage.None, certainty);
}
