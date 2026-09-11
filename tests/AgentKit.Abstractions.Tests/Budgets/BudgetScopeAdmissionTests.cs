// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Budgets;



/// <summary>Verifies BudgetScopeAdmission behavior and contracts.</summary>
public sealed class BudgetScopeAdmissionTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void BudgetScopeAdmission_WhenDefaultLifetimeIsNotPositive_ThrowsExactParameterName(int ticks)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new BudgetScopeAdmission(1, 1, TimeSpan.FromTicks(ticks)));
        exception.ParamName.ShouldBe("defaultReservationLifetime");
    }

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(-1, 1, 1)]
    [InlineData(1, 0, 1)]
    [InlineData(1, -1, 1)]
    [InlineData(1, 1, 0)]
    [InlineData(1, 1, -1)]
    public void BudgetScopeAdmission_WhenAnyCapturedBoundIsNotPositive_ThrowsArgumentOutOfRangeException(int maximumScopeDepth, int maximumOpenReservations, int lifetimeTicks)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new BudgetScopeAdmission(maximumScopeDepth, maximumOpenReservations, TimeSpan.FromTicks(lifetimeTicks)));
        exception.ParamName.ShouldBe(maximumScopeDepth <= 0 ? "maximumScopeDepth" : maximumOpenReservations <= 0 ? "maximumOpenReservationsPerScope" : "defaultReservationLifetime");
    }

    [Fact]
    public void BudgetScopeAdmission_WhenCapturedBoundsAreAtPositiveBoundary_PreservesAllFields()
    {
        var admission = new BudgetScopeAdmission(1, 1, TimeSpan.FromTicks(1));
        admission.MaximumScopeDepth.ShouldBe(1);
        admission.MaximumOpenReservationsPerScope.ShouldBe(1);
        admission.DefaultReservationLifetime.ShouldBe(TimeSpan.FromTicks(1));
    }

    [Fact]
    public void BudgetScopeAdmission_WhenOverrunPolicyIsUndefined_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new BudgetScopeAdmission(1, 1, TimeSpan.FromMinutes(1), (BudgetOverrunHoldPolicy) int.MaxValue));
        exception.ParamName.ShouldBe("overrunHoldPolicy");
    }
}
