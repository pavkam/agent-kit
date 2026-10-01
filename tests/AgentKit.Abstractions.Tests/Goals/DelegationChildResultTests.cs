// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Goals;

/// <summary>Verifies DelegationChildResult constraints and value semantics.</summary>
public sealed class DelegationChildResultTests
{
    private static DelegationChildResult Make(
        DelegationStatus status = DelegationStatus.Dispatched,
        StructuredGoalResult? result = null,
        SessionId? session = null,
        ImmutableArray<EvidenceReference>? evidence = null) => new(
            new DelegationId(Guid.NewGuid()), new GoalId(Guid.NewGuid()), new AgentId(Guid.NewGuid()), session, null, null,
            status, result, evidence ?? [], GoalBudgetUsage.None, SideEffectCertainty.Unknown, ExtensionData.Empty);

    [Fact]
    public void Constructor_WhenDispatched_AllowsAbsentSessionAttemptAndRun()
    {
        var result = Make();

        result.ChildSessionId.ShouldBeNull();
        result.ChildAttemptId.ShouldBeNull();
        result.ChildRunId.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WhenDispatchedWithAResult_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => Make(result: new StructuredGoalResult("s", ExtensionData.Empty))).ParamName.ShouldBe("result");

    [Fact]
    public void Constructor_WhenStatusIsUndefined_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Make((DelegationStatus) 99)).ParamName.ShouldBe("status");

    [Fact]
    public void Constructor_WhenASuppliedSessionIsDefault_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Make(session: default(SessionId))).ParamName.ShouldBe("childSessionId");

    [Fact]
    public void Constructor_WhenEvidenceIsDefault_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => Make(evidence: default(ImmutableArray<EvidenceReference>))).ParamName.ShouldBe("evidence");

    [Fact]
    public void Equality_WhenEvidenceMatchesByContent_IsStructural()
    {
        var id = new DelegationId(Guid.NewGuid());
        var goal = new GoalId(Guid.NewGuid());
        var agent = new AgentId(Guid.NewGuid());
        DelegationChildResult Build() => new(
            id, goal, agent, null, null, null, DelegationStatus.Succeeded, null,
            [new EvidenceReference("k", "r")], GoalBudgetUsage.None, SideEffectCertainty.Unknown, ExtensionData.Empty);

        Build().ShouldBe(Build());
    }
}
