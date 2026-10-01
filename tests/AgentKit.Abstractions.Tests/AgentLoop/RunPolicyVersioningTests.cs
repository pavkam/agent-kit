// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.AgentLoop;

/// <summary>Verifies RunPolicyVersioning behavior and contracts.</summary>
public sealed class RunPolicyVersioningTests
{
    [Fact]
    public void Compute_WhenMaxTurnsIsNotPositive_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => RunPolicyVersioning.Compute(0, TimeSpan.FromMinutes(1), Key()));
        exception.ParamName.ShouldBe("maxTurns");
    }

    [Fact]
    public void Compute_WhenAttemptTimeoutIsNotPositive_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => RunPolicyVersioning.Compute(8, TimeSpan.Zero, Key()));
        exception.ParamName.ShouldBe("attemptTimeout");
    }

    [Fact]
    public void Compute_WhenContinuationPolicyKeyIsBlank_ThrowsExactArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(
            () => RunPolicyVersioning.Compute(8, TimeSpan.FromMinutes(1), default));
        exception.ParamName.ShouldBe("continuationPolicyKey");
    }

    [Fact]
    public void Compute_WhenNoRestrictionIsGiven_ReturnsTheVersionItAlwaysDid()
    {
        var plain = RunPolicyVersioning.Compute(8, TimeSpan.FromMinutes(2), Key());

        RunPolicyVersioning.Compute(8, TimeSpan.FromMinutes(2), Key(), allowedTools: null, budgetParentScopeId: null).ShouldBe(plain);
    }

    [Fact]
    public void Compute_WhenAllowListDiffersFromNoneOrFromAnotherList_ReturnsADifferentVersion()
    {
        var none = RunPolicyVersioning.Compute(8, TimeSpan.FromMinutes(2), Key());
        var empty = RunPolicyVersioning.Compute(8, TimeSpan.FromMinutes(2), Key(), allowedTools: []);
        var read = RunPolicyVersioning.Compute(8, TimeSpan.FromMinutes(2), Key(), allowedTools: [new ToolId("read")]);
        var write = RunPolicyVersioning.Compute(8, TimeSpan.FromMinutes(2), Key(), allowedTools: [new ToolId("write")]);

        new[] { none, empty, read, write }.Distinct().Count().ShouldBe(4);
    }

    [Fact]
    public void Compute_WhenAllowListIsReordered_ReturnsTheSameVersion()
    {
        var first = RunPolicyVersioning.Compute(8, TimeSpan.FromMinutes(2), Key(), allowedTools: [new ToolId("a"), new ToolId("b")]);
        var second = RunPolicyVersioning.Compute(8, TimeSpan.FromMinutes(2), Key(), allowedTools: [new ToolId("b"), new ToolId("a")]);

        second.ShouldBe(first);
    }

    [Fact]
    public void Compute_WhenBudgetParentScopeDiffers_ReturnsADifferentVersion()
    {
        var none = RunPolicyVersioning.Compute(8, TimeSpan.FromMinutes(2), Key());
        var first = RunPolicyVersioning.Compute(8, TimeSpan.FromMinutes(2), Key(), budgetParentScopeId: new BudgetScopeId(Guid.Parse("d0000000-0000-0000-0000-000000000001")));
        var second = RunPolicyVersioning.Compute(8, TimeSpan.FromMinutes(2), Key(), budgetParentScopeId: new BudgetScopeId(Guid.Parse("d0000000-0000-0000-0000-000000000002")));

        new[] { none, first, second }.Distinct().Count().ShouldBe(3);
    }

    [Fact]
    public void Compute_WhenCalledTwiceWithIdenticalArguments_ReturnsTheSameVersion()
    {
        var first = RunPolicyVersioning.Compute(8, TimeSpan.FromMinutes(2), Key());
        var second = RunPolicyVersioning.Compute(8, TimeSpan.FromMinutes(2), Key());

        first.ShouldBe(second);
    }

    [Fact]
    public void Compute_WhenMaxTurnsDiffers_ReturnsADifferentVersion() =>
        RunPolicyVersioning.Compute(8, TimeSpan.FromMinutes(2), Key())
            .ShouldNotBe(RunPolicyVersioning.Compute(9, TimeSpan.FromMinutes(2), Key()));

    [Fact]
    public void Compute_WhenAttemptTimeoutDiffers_ReturnsADifferentVersion() =>
        RunPolicyVersioning.Compute(8, TimeSpan.FromMinutes(2), Key())
            .ShouldNotBe(RunPolicyVersioning.Compute(8, TimeSpan.FromMinutes(3), Key()));

    [Fact]
    public void Compute_WhenContinuationPolicyKeyDiffers_ReturnsADifferentVersion() =>
        RunPolicyVersioning.Compute(8, TimeSpan.FromMinutes(2), Key("a"))
            .ShouldNotBe(RunPolicyVersioning.Compute(8, TimeSpan.FromMinutes(2), Key("b")));

    [Fact]
    public void Compute_Always_ReturnsAPositiveVersion() =>
        RunPolicyVersioning.Compute(8, TimeSpan.FromMinutes(2), Key()).Value.ShouldBeGreaterThan(0);

    private static ComponentKey<IRunContinuationPolicy> Key(string value = "continuation") => new(value);
}
