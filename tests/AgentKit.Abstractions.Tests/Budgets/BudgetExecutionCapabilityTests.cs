// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Budgets;

public sealed class BudgetExecutionCapabilityTests
{
    private static readonly AgentId _agentId = new(Guid.Parse("10000000-0000-0000-0000-000000000001"));
    private static readonly SessionId _sessionId = new(Guid.Parse("20000000-0000-0000-0000-000000000002"));
    private static readonly RunId _runId = new(Guid.Parse("30000000-0000-0000-0000-000000000003"));
    private static readonly OperationId _operationId = new(Guid.Parse("40000000-0000-0000-0000-000000000004"));

    [Fact]
    public void Constructor_WhenBindingMatches_PreservesBorrowedScopeAndEvidence()
    {
        var identity = Identity();
        var correlation = new InRunOperationCorrelation(_operationId, _runId, null);
        var scope = Scope(Address(runId: _runId, operationId: _operationId));

        var capability = new BudgetExecutionCapability(
            new BudgetProfileKey("standard"), new BudgetProfileVersion(7), identity, correlation, scope);

        capability.ProfileKey.ShouldBe(new BudgetProfileKey("standard"));
        capability.ProfileVersion.ShouldBe(new BudgetProfileVersion(7));
        capability.Identity.ShouldBeSameAs(identity);
        capability.Correlation.ShouldBeSameAs(correlation);
        capability.Scope.ShouldBeSameAs(scope);
    }

    [Theory]
    [InlineData("before", false)]
    [InlineData("after", false)]
    [InlineData("in", true)]
    public void Constructor_WhenCorrelationStageMatchesAddress_AcceptsRequiredRunShape(string stage, bool hasRun)
    {
        var correlation = Correlation(stage);
        var scope = Scope(Address(runId: hasRun ? _runId : null, operationId: _operationId));

        var capability = new BudgetExecutionCapability(
            new BudgetProfileKey("standard"), new BudgetProfileVersion(1), Identity(), correlation, scope);

        capability.Scope.ShouldBeSameAs(scope);
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("principal")]
    [InlineData("operation-missing")]
    [InlineData("operation-other")]
    [InlineData("before-run-present")]
    [InlineData("after-run-present")]
    [InlineData("in-run-missing")]
    [InlineData("in-run-other")]
    public void Constructor_WhenScopeBindingDiffers_ThrowsExactScopeParameter(string mismatch)
    {
        var identity = Identity();
        var correlation = mismatch switch
        {
            "before-run-present" => Correlation("before"),
            "after-run-present" => Correlation("after"),
            _ => Correlation("in"),
        };
        var address = Address(
            tenant: mismatch == "tenant" ? "other" : "tenant",
            principal: mismatch == "principal" ? "other" : "principal",
            runId: mismatch is "in-run-missing" ? null : mismatch == "in-run-other" ? OtherRunId() : _runId,
            operationId: mismatch == "operation-missing" ? null : mismatch == "operation-other" ? OtherOperationId() : _operationId);

        var exception = Should.Throw<ArgumentException>(() => new BudgetExecutionCapability(
            new BudgetProfileKey("standard"), new BudgetProfileVersion(1), identity, correlation, Scope(address)));

        exception.ParamName.ShouldBe("scope");
    }

    [Fact]
    public void Constructor_WhenScopeIdIsDefault_ThrowsExactScopeParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new BudgetExecutionCapability(
            new BudgetProfileKey("standard"), new BudgetProfileVersion(1), Identity(), Correlation("in"),
            new TestBudgetScope(default, Address(runId: _runId, operationId: _operationId))));

        exception.ParamName.ShouldBe("scope");
    }

    [Theory]
    [InlineData("profileKey", typeof(ArgumentNullException))]
    [InlineData("profileVersion", typeof(ArgumentOutOfRangeException))]
    [InlineData("identity", typeof(ArgumentNullException))]
    [InlineData("correlation", typeof(ArgumentNullException))]
    [InlineData("scope", typeof(ArgumentNullException))]
    public void Constructor_WhenRequiredValueIsInvalid_ThrowsExactParameter(string parameter, Type exceptionType)
    {
        var exception = Record.Exception(() => new BudgetExecutionCapability(
            parameter == "profileKey" ? default : new BudgetProfileKey("standard"),
            parameter == "profileVersion" ? default : new BudgetProfileVersion(1),
            parameter == "identity" ? null! : Identity(),
            parameter == "correlation" ? null! : Correlation("in"),
            parameter == "scope" ? null! : Scope(Address(runId: _runId, operationId: _operationId))));

        _ = exception.ShouldNotBeNull();
        exception.GetType().ShouldBe(exceptionType);
        ((ArgumentException) exception).ParamName.ShouldBe(parameter);
    }

    [Fact]
    public void Constructor_WhenScopeAddressIsNull_ThrowsExactScopeParameter()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new BudgetExecutionCapability(
            new BudgetProfileKey("standard"), new BudgetProfileVersion(1), Identity(), Correlation("in"),
            new NullAddressBudgetScope()));

        exception.ParamName.ShouldBe("scope");
    }

    [Fact]
    public void ThrowIfInvalidBudgetExecutionBinding_WhenReferenceIsNull_ThrowsInferredParameterNames()
    {
        BudgetScopeAddress address = null!;
        ExecutionIdentity identity = null!;
        OperationCorrelation correlation = null!;

        Should.Throw<ArgumentNullException>(
            () => ArgumentException.ThrowIfInvalidBudgetExecutionBinding(address, Identity(), Correlation("in")))
            .ParamName.ShouldBe("address");
        Should.Throw<ArgumentNullException>(
            () => ArgumentException.ThrowIfInvalidBudgetExecutionBinding(
                Address(runId: _runId, operationId: _operationId), identity, Correlation("in")))
            .ParamName.ShouldBe("identity");
        Should.Throw<ArgumentNullException>(
            () => ArgumentException.ThrowIfInvalidBudgetExecutionBinding(
                Address(runId: _runId, operationId: _operationId), Identity(), correlation))
            .ParamName.ShouldBe("correlation");
    }

    [Fact]
    public void ThrowIfInvalidBudgetExecutionBinding_WhenBindingDiffers_UsesExplicitParameterName()
    {
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfInvalidBudgetExecutionBinding(
            Address(tenant: "other", runId: _runId, operationId: _operationId),
            Identity(),
            Correlation("in"),
            "binding"));

        exception.ParamName.ShouldBe("binding");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void BudgetProfileVersion_WhenNotPositive_ThrowsExactValueParameter(long value)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new BudgetProfileVersion(value));

        exception.ParamName.ShouldBe("value");
    }

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
    public void BudgetScopeAddress_WhenConstructorIdentityIsDefault_ThrowsExactParameter(
        string parameter,
        Type exceptionType)
    {
        var exception = Record.Exception(() => new BudgetScopeAddress(
            parameter == "tenantId" ? default : new TenantId("tenant"),
            parameter == "principalId" ? default : new PrincipalId("principal"),
            parameter == "agentId" ? default : _agentId,
            parameter == "sessionId" ? default : _sessionId,
            parameter == "runId" ? default : _runId,
            parameter == "operationId" ? default : _operationId));

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

    private static BudgetScopeAddress Address(
        string tenant = "tenant",
        string principal = "principal",
        RunId? runId = null,
        OperationId? operationId = null) =>
        new(new TenantId(tenant), new PrincipalId(principal), _agentId, _sessionId, runId, operationId);

    private static OperationCorrelation Correlation(string stage) => stage switch
    {
        "before" => new BeforeRunOperationCorrelation(_operationId, null),
        "in" => new InRunOperationCorrelation(_operationId, _runId, null),
        "after" => new AfterRunOperationCorrelation(_operationId, _runId),
        _ => throw new ArgumentOutOfRangeException(nameof(stage)),
    };

    private static ExecutionIdentity Identity() => TestSupport.TestExecutionIdentity.Create(
        new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);

    private static TestBudgetScope Scope(BudgetScopeAddress address) =>
        new(new BudgetScopeId(Guid.Parse("50000000-0000-0000-0000-000000000005")), address);

    private static RunId OtherRunId() => new(Guid.Parse("60000000-0000-0000-0000-000000000006"));

    private static OperationId OtherOperationId() => new(Guid.Parse("70000000-0000-0000-0000-000000000007"));
}
