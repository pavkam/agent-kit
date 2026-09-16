// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Budgets;



/// <summary>Verifies BudgetLedgerScopeReference behavior and contracts.</summary>
public sealed class BudgetLedgerScopeReferenceTests
{
    [Fact]
    public void BudgetLedgerScopeReference_WhenIdIsDefault_ThrowsExactParameterName()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new BudgetLedgerScopeReference(default, Address()));
        exception.ParamName.ShouldBe("id");
    }

    [Fact]
    public void BudgetLedgerScopeReference_WhenAddressIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new BudgetLedgerScopeReference(new BudgetScopeId(Guid.NewGuid()), null!));
        exception.ParamName.ShouldBe("address");
    }

    [Fact]
    public void BudgetLedgerScopeReference_WhenInputsAreValid_PreservesAllFields()
    {
        var id = new BudgetScopeId(Guid.NewGuid());
        var address = Address();
        var reference = new BudgetLedgerScopeReference(id, address);
        reference.Id.ShouldBe(id);
        reference.Address.ShouldBe(address);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new BudgetLedgerScopeReference(new BudgetScopeId(Guid.NewGuid()), Address());
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static BudgetScopeAddress Address(OperationId? operationId = null) => new(new TenantId("tenant"), new PrincipalId("principal"), new AgentId(Guid.NewGuid()), null, null, operationId);
}
