// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Json.Tests;

/// <summary>Verifies the internal mutable in-memory projection of one replayed scope.</summary>
public sealed class ScopeStateTests
{
    private static readonly BudgetScopeAddress _address = new(
        new TenantId("tenant"), new PrincipalId("principal"), new AgentId(Guid.NewGuid()), null, null, null);

    /// <summary>Verifies construction refuses a null reference or request, attributing the exact parameter name.</summary>
    [Fact]
    public void Constructor_WhenReferenceOrRequestIsNull_ThrowsWithExactParameterName()
    {
        var reference = CreateReference();
        var request = CreateRequest();

        Should.Throw<ArgumentNullException>(() => new ScopeState(null!, request, null, 1)).ParamName.ShouldBe("reference");
        Should.Throw<ArgumentNullException>(() => new ScopeState(reference, null!, null, 1)).ParamName.ShouldBe("request");
    }

    /// <summary>Verifies construction refuses a non-positive depth.</summary>
    [Fact]
    public void Constructor_WhenDepthIsNotPositive_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ScopeState(CreateReference(), CreateRequest(), null, 0))
            .ParamName.ShouldBe("depth");

    /// <summary>Verifies a root scope exposes the exact captured reference, request, depth, and starts with empty mutable collections.</summary>
    [Fact]
    public void Constructor_WhenArgumentsAreValidForRootScope_ExposesExactCapturedValues()
    {
        var reference = CreateReference();
        var request = CreateRequest();

        var state = new ScopeState(reference, request, null, 1);

        state.Reference.ShouldBeSameAs(reference);
        state.Request.ShouldBeSameAs(request);
        state.Parent.ShouldBeNull();
        state.Depth.ShouldBe(1);
        state.Reservations.ShouldBeEmpty();
        state.OverrunHolds.ShouldBeEmpty();
    }

    /// <summary>Verifies a child scope retains its parent link and greater depth.</summary>
    [Fact]
    public void Constructor_WhenArgumentsAreValidForChildScope_RetainsParentLink()
    {
        var parent = new ScopeState(CreateReference(), CreateRequest(), null, 1);

        var child = new ScopeState(CreateReference(), CreateRequest(), parent, 2);

        child.Parent.ShouldBeSameAs(parent);
        child.Depth.ShouldBe(2);
    }

    private static BudgetLedgerScopeReference CreateReference() =>
        new(new BudgetScopeId(Guid.NewGuid()), _address);

    private static BudgetLedgerScopeCreateRequest CreateRequest() => new(
        new BudgetScopeRequest(null, _address, [], new IdempotencyKey($"scope-state-{Guid.NewGuid():N}")),
        new BudgetScopeAdmission(8, 32, TimeSpan.FromMinutes(5), BudgetOverrunHoldPolicy.ClearWhenReconciled));
}
