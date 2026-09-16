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

    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var parentScopeId = new BudgetScopeId(Guid.NewGuid());
        var address = Address();
        ImmutableArray<BudgetLimit> limits = [Limit()];
        var key = new IdempotencyKey("scope");
        var request = new BudgetScopeRequest(parentScopeId, address, limits, key);
        request.ParentScopeId.ShouldBe(parentScopeId);
        request.Address.ShouldBe(address);
        request.Limits.ShouldBe(limits);
        request.IdempotencyKey.ShouldBe(key);
    }

    [Fact]
    public void BudgetScopeRequest_WhenCopiedAddressIsNull_RejectsCopy()
    {
        var originalRequest = ScopeRequest();
        var exception = Should.Throw<ArgumentNullException>(() => originalRequest with { Address = null! });
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void Equals_WhenAllFieldsMatch_InstancesAreEqual()
    {
        var address = Address();
        ImmutableArray<BudgetLimit> limits = [Limit()];
        var key = new IdempotencyKey("scope");
        var first = new BudgetScopeRequest(null, address, limits, key);
        var second = new BudgetScopeRequest(null, address, limits, key);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void Equals_WhenLimitsDiffer_IsNotEqual()
    {
        var address = Address();
        var key = new IdempotencyKey("scope");
        var first = new BudgetScopeRequest(null, address, [Limit()], key);
        var second = new BudgetScopeRequest(null, address, [], key);
        first.ShouldNotBe(second);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new BudgetScopeRequest(null, Address(), [Limit()], new IdempotencyKey("scope"));
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static BudgetScopeAddress Address(OperationId? operationId = null) => new(new TenantId("tenant"), new PrincipalId("principal"), new AgentId(Guid.NewGuid()), null, null, operationId);
    private static BudgetScopeRequest ScopeRequest() => new(null, Address(), [], new IdempotencyKey("scope"));
    private static BudgetLimit Limit() => new(new BudgetDimension("tests.requests"), 1m, new BudgetUnit("requests"), BudgetLimitKind.Hard);
}
