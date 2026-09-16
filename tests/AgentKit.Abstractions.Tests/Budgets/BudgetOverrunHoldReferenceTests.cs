// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Budgets;



/// <summary>Verifies BudgetOverrunHoldReference behavior and contracts.</summary>
public sealed class BudgetOverrunHoldReferenceTests
{
    private static BudgetLedgerScopeReference Scope(OperationId? operationId = null) => new(new BudgetScopeId(Guid.NewGuid()), Address(operationId));
    [Fact]
    public void BudgetOverrunHoldReference_WhenRevisionIsDefault_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new BudgetOverrunHoldReference(Scope(), Reservation(), default));
        exception.ParamName.ShouldBe("triggeringRevision");
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("principal")]
    [InlineData("agent")]
    [InlineData("session")]
    [InlineData("run")]
    [InlineData("operation")]
    public void BudgetOverrunHoldReference_WhenBoundaryContradictsChargedAddress_ThrowsExactParameterName(string mismatch)
    {
        var tenant = new TenantId("tenant");
        var principal = new PrincipalId("principal");
        var agent = new AgentId(Guid.NewGuid());
        var session = new SessionId(Guid.NewGuid());
        var run = new RunId(Guid.NewGuid());
        var operation = new OperationId(Guid.NewGuid());
        var charged = new BudgetScopeAddress(tenant, principal, agent, session, run, operation);
        var boundary = mismatch switch
        {
            "tenant" => new BudgetScopeAddress(new TenantId("other-tenant"), principal, agent, session, run, operation),
            "principal" => new BudgetScopeAddress(tenant, new PrincipalId("other-principal"), agent, session, run, operation),
            "agent" => new BudgetScopeAddress(tenant, principal, new AgentId(Guid.NewGuid()), session, run, operation),
            "session" => new BudgetScopeAddress(tenant, principal, agent, new SessionId(Guid.NewGuid()), run, operation),
            "run" => new BudgetScopeAddress(tenant, principal, agent, session, new RunId(Guid.NewGuid()), operation),
            "operation" => new BudgetScopeAddress(tenant, principal, agent, session, run, new OperationId(Guid.NewGuid())),
            _ => throw new ArgumentOutOfRangeException(nameof(mismatch)),
        };
        var reservationScope = new BudgetLedgerScopeReference(new BudgetScopeId(Guid.NewGuid()), charged);
        var reservation = new BudgetLedgerReservationReference(reservationScope, ReservationId());
        var boundaryScope = new BudgetLedgerScopeReference(new BudgetScopeId(Guid.NewGuid()), boundary);
        var exception = Should.Throw<ArgumentException>(() => new BudgetOverrunHoldReference(boundaryScope, reservation, new BudgetAccountingRevision(1)));
        exception.ParamName.ShouldBe("reservation");
    }

    [Fact]
    public void BudgetOverrunHoldReference_WhenBoundaryNarrowsOnlyDescendantFacts_PreservesCanonicalBinding()
    {
        var tenant = new TenantId("tenant");
        var principal = new PrincipalId("principal");
        var agent = new AgentId(Guid.NewGuid());
        var charged = new BudgetScopeAddress(tenant, principal, agent, new SessionId(Guid.NewGuid()), new RunId(Guid.NewGuid()), new OperationId(Guid.NewGuid()));
        var boundary = new BudgetScopeAddress(tenant, principal, agent, null, null, null);
        var reservationScope = new BudgetLedgerScopeReference(new BudgetScopeId(Guid.NewGuid()), charged);
        var reservation = new BudgetLedgerReservationReference(reservationScope, ReservationId());
        var boundaryScope = new BudgetLedgerScopeReference(new BudgetScopeId(Guid.NewGuid()), boundary);
        var hold = new BudgetOverrunHoldReference(boundaryScope, reservation, new BudgetAccountingRevision(1));
        BudgetOverrunSecurityBinding.Resource(hold).Kind.ShouldBe(ProtectedResourceKind.ApplicationState);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var scope = Scope();
        var original = new BudgetOverrunHoldReference(scope, new BudgetLedgerReservationReference(scope, ReservationId()), new BudgetAccountingRevision(1));
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static BudgetScopeAddress Address(OperationId? operationId = null) => new(new TenantId("tenant"), new PrincipalId("principal"), new AgentId(Guid.NewGuid()), null, null, operationId);
    private static BudgetLedgerReservationReference Reservation() => new(Scope(), ReservationId());
    private static BudgetReservationId ReservationId() => new(Guid.NewGuid());
}
