// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Budgets;



/// <summary>Verifies BudgetScopeAddress behavior and contracts.</summary>
public sealed class BudgetScopeAddressTests
{
    private static readonly AgentId _agentId = new(Guid.Parse("10000000-0000-0000-0000-000000000001"));
    private static readonly SessionId _sessionId = new(Guid.Parse("20000000-0000-0000-0000-000000000002"));
    private static readonly RunId _runId = new(Guid.Parse("30000000-0000-0000-0000-000000000003"));
    private static readonly OperationId _operationId = new(Guid.Parse("40000000-0000-0000-0000-000000000004"));
    [Fact]
    public void BudgetScopeAddress_WhenCopiedWithDefaultRequiredIdentity_RejectsCopyBypass()
    {
        var address = Address(runId: null, operationId: null);
        var tenant = Should.Throw<ArgumentNullException>(() => address with { TenantId = default });
        var principal = Should.Throw<ArgumentNullException>(() => address with { PrincipalId = default });
        var agent = Should.Throw<ArgumentOutOfRangeException>(() => address with { AgentId = default });
        tenant.ParamName.ShouldBe("tenantId");
        principal.ParamName.ShouldBe("principalId");
        agent.ParamName.ShouldBe("agentId");
    }

    [Fact]
    public void BudgetScopeAddress_WhenCopiedWithDefaultOptionalIdentity_RejectsCopyBypass()
    {
        var address = Address(runId: null, operationId: null);
        Should.Throw<ArgumentOutOfRangeException>(() => address with { SessionId = default(SessionId) }).ParamName.ShouldBe("sessionId");
        Should.Throw<ArgumentOutOfRangeException>(() => address with { RunId = default(RunId) }).ParamName.ShouldBe("runId");
        Should.Throw<ArgumentOutOfRangeException>(() => address with { OperationId = default(OperationId) }).ParamName.ShouldBe("operationId");
    }

    [Theory]
    [InlineData("tenantId", typeof(ArgumentNullException))]
    [InlineData("principalId", typeof(ArgumentNullException))]
    [InlineData("agentId", typeof(ArgumentOutOfRangeException))]
    [InlineData("sessionId", typeof(ArgumentOutOfRangeException))]
    [InlineData("runId", typeof(ArgumentOutOfRangeException))]
    [InlineData("operationId", typeof(ArgumentOutOfRangeException))]
    public void BudgetScopeAddress_WhenConstructorIdentityIsDefault_ThrowsExactParameter(string parameter, Type exceptionType)
    {
        var exception = Record.Exception(() => new BudgetScopeAddress(parameter == "tenantId" ? default : new TenantId("tenant"), parameter == "principalId" ? default : new PrincipalId("principal"), parameter == "agentId" ? default : _agentId, parameter == "sessionId" ? default : _sessionId, parameter == "runId" ? default : _runId, parameter == "operationId" ? default : _operationId));
        _ = exception.ShouldNotBeNull();
        exception.GetType().ShouldBe(exceptionType);
        ((ArgumentException) exception).ParamName.ShouldBe(parameter);
    }

    [Fact]
    public void BudgetScopeAddress_WhenParentHasNoRunOrOperation_PreservesLegitimateParentAddress()
    {
        var address = Address(runId: null, operationId: null);
        address.RunId.ShouldBeNull();
        address.OperationId.ShouldBeNull();
        address.AgentId.ShouldBe(_agentId);
        address.SessionId.ShouldBe(_sessionId);
    }

    private static BudgetScopeAddress Address(string tenant = "tenant", string principal = "principal", RunId? runId = null, OperationId? operationId = null) => new(new TenantId(tenant), new PrincipalId(principal), _agentId, _sessionId, runId, operationId);
}
