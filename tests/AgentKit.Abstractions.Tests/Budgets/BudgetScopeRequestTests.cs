// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Budgets;



/// <summary>Verifies BudgetScopeRequest behavior and contracts.</summary>
public sealed class BudgetScopeRequestTests
{
    [Fact]
    public void BudgetScopeRequest_WhenConstructorParentIsDefault_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new BudgetScopeRequest(new BudgetScopeId(), Address(), [], new IdempotencyKey("scope")));
        exception.ParamName.ShouldBe("parentScopeId");
    }

    [Fact]
    public void BudgetScopeRequest_WhenConstructorIdempotencyKeyIsDefault_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new BudgetScopeRequest(null, Address(), [], default));
        exception.ParamName.ShouldBe("idempotencyKey");
    }

    [Fact]
    public void BudgetScopeRequest_WhenLimitsContainNull_RejectsBeforeAssignment()
    {
        var exception = Should.Throw<ArgumentException>(() => new BudgetScopeRequest(null, Address(), [null!], new IdempotencyKey("scope")));
        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("limits");
    }

    [Fact]
    public void BudgetScopeRequest_WhenLimitsRepeatDimension_RejectsBeforeAssignment()
    {
        var first = Limit();
        var second = new BudgetLimit(first.Dimension, 2m, first.Unit, BudgetLimitKind.Soft);
        var exception = Should.Throw<ArgumentException>(() => new BudgetScopeRequest(null, Address(), [first, second], new IdempotencyKey("scope")));
        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("limits");
    }

    [Fact]
    public void BudgetScopeRequest_WhenCopiedLimitsRepeatDimension_RejectsCopy()
    {
        var request = ScopeRequest();
        var limit = Limit();
        var exception = Should.Throw<ArgumentException>(() => request with { Limits = [limit, limit] });
        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("Limits");
    }

    [Fact]
    public void BudgetScopeRequest_WhenCopiedIdempotencyKeyIsDefault_RejectsCopy()
    {
        var originalRequest = ScopeRequest();
        var exception = Should.Throw<ArgumentNullException>(() => originalRequest with { IdempotencyKey = default });
        exception.ParamName.ShouldBe("IdempotencyKey");
    }

    [Fact]
    public void BudgetScopeRequest_WhenCopiedParentScopeIdIsPresentAndDefault_RejectsCopy()
    {
        var originalRequest = ScopeRequest();
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => originalRequest with { ParentScopeId = new BudgetScopeId() });
        exception.ParamName.ShouldBe("ParentScopeId");
    }

    private static BudgetScopeAddress Address(OperationId? operationId = null) => new(new TenantId("tenant"), new PrincipalId("principal"), new AgentId(Guid.NewGuid()), null, null, operationId);
    private static BudgetScopeRequest ScopeRequest() => new(null, Address(), [], new IdempotencyKey("scope"));
    private static BudgetLimit Limit() => new(new BudgetDimension("tests.requests"), 1m, new BudgetUnit("requests"), BudgetLimitKind.Hard);
}
