// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Json.Tests;

/// <summary>Verifies the portable JSON mirror of the capacity policy frozen when a scope was admitted.</summary>
public sealed class JsonBudgetScopeAdmissionTests
{
    /// <summary>Verifies projecting a null domain admission is refused.</summary>
    [Fact]
    public void FromDomain_WhenValueIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => JsonBudgetScopeAdmission.FromDomain(null!)).ParamName.ShouldBe("value");

    /// <summary>Verifies a round trip through <see cref="JsonBudgetScopeAdmission.FromDomain"/> and <see cref="JsonBudgetScopeAdmission.ToDomain"/> reproduces an equal domain value.</summary>
    [Fact]
    public void ToDomain_WhenProjectedFromDomain_RoundTripsExactly()
    {
        var original = new BudgetScopeAdmission(
            8, 32, TimeSpan.FromMinutes(5), BudgetOverrunHoldPolicy.RequireAuthorizedResolution);

        var restored = JsonBudgetScopeAdmission.FromDomain(original).ToDomain();

        restored.ShouldBe(original);
    }

    /// <summary>Verifies a non-positive persisted depth bound is rejected during reconstruction.</summary>
    [Fact]
    public void ToDomain_WhenMaximumScopeDepthIsNotPositive_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(
            () => new JsonBudgetScopeAdmission(0, 32, TimeSpan.FromMinutes(5), BudgetOverrunHoldPolicy.ClearWhenReconciled)
                .ToDomain());

    /// <summary>Verifies an undefined persisted overrun policy is rejected during reconstruction.</summary>
    [Fact]
    public void ToDomain_WhenOverrunHoldPolicyIsUndefined_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(
            () => new JsonBudgetScopeAdmission(8, 32, TimeSpan.FromMinutes(5), (BudgetOverrunHoldPolicy) 9_999)
                .ToDomain());

    /// <summary>Verifies the document survives an actual JSON encode and decode round trip.</summary>
    [Fact]
    public void Serialization_WhenRoundTripped_PreservesEveryField()
    {
        var original = new JsonBudgetScopeAdmission(
            8, 32, TimeSpan.FromMinutes(5), BudgetOverrunHoldPolicy.RequireAuthorizedResolution);
        var options = JsonStoreSerialization.CreateCanonicalOptions();

        var decoded = JsonStoreSerialization.Decode<JsonBudgetScopeAdmission>(
            JsonStoreSerialization.Encode(original, options, 1_024), options);

        decoded.ShouldBe(original);
    }
}
