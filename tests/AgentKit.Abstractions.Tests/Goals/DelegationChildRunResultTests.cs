// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Goals;

/// <summary>Verifies DelegationChildRunResult constraints.</summary>
public sealed class DelegationChildRunResultTests
{
    [Fact]
    public void Constructor_WhenValid_RetainsEveryFact()
    {
        var run = new RunId(Guid.NewGuid());

        var result = new DelegationChildRunResult(run, DelegationStatus.Succeeded, "done", new GoalBudgetUsage(1, 2, 0), SideEffectCertainty.DefinitelyPerformed);

        result.RunId.ShouldBe(run);
        result.Status.ShouldBe(DelegationStatus.Succeeded);
        result.Summary.ShouldBe("done");
        result.Usage.ToolCalls.ShouldBe(2);
        result.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyPerformed);
    }

    [Fact]
    public void Constructor_WhenNoRunWasAdmitted_AllowsANullRun() =>
        new DelegationChildRunResult(null, DelegationStatus.Failed, null, GoalBudgetUsage.None, SideEffectCertainty.DefinitelyNotPerformed).RunId.ShouldBeNull();

    [Fact]
    public void Constructor_WhenRunIsPresentButDefault_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new DelegationChildRunResult(default(RunId), DelegationStatus.Failed, null, GoalBudgetUsage.None, SideEffectCertainty.Unknown)).ParamName.ShouldBe("runId");

    [Theory]
    [InlineData((DelegationStatus) 99)]
    [InlineData(DelegationStatus.Dispatched)]
    public void Constructor_WhenStatusIsNotTerminal_ThrowsArgumentOutOfRangeException(DelegationStatus status) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new DelegationChildRunResult(null, status, null, GoalBudgetUsage.None, SideEffectCertainty.Unknown)).ParamName.ShouldBe("status");

    [Fact]
    public void Constructor_WhenUsageIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new DelegationChildRunResult(null, DelegationStatus.Failed, null, null!, SideEffectCertainty.Unknown)).ParamName.ShouldBe("usage");

    [Fact]
    public void Constructor_WhenCertaintyIsUndefined_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new DelegationChildRunResult(null, DelegationStatus.Failed, null, GoalBudgetUsage.None, (SideEffectCertainty) 99)).ParamName.ShouldBe("sideEffectCertainty");
}
