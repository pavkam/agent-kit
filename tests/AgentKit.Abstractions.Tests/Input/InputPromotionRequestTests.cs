// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Input;



/// <summary>Verifies InputPromotionRequest behavior and contracts.</summary>
public sealed class InputPromotionRequestTests
{
    [Fact]
    public void InputPromotionRequest_WhenCommittedBoundaryReusesTargetTurn_ThrowsArgumentExceptionWithParamName()
    {
        var exception = ShouldThrowExactly<ArgumentException>(() => new InputPromotionRequest(Agent(), Session(), Lane(), Operation(), new OperationStateRevision(1), new SessionBranchCursor(Branch(), null), new SessionSequence(1), null, null, Identity(), Authorization(Identity(), Agent(), Session(), Operation()), PromotionBoundary.AfterTurnCommitted, Turn(), Turn(), 1));
        exception.ParamName.ShouldBe("previousTurnId");
    }

    [Fact]
    public void InputPromotionRequest_WhenInvocationUsesSameRunAndDifferentOperation_IsValid()
    {
        var invocation = new InRunOperationCorrelation(new OperationId(Guid.Parse("80000000-0000-0000-0000-000000000001")), Operation().RunId, Turn());
        var request = PromotionRequest(Authorization(Identity(), Agent(), Session(), invocation));
        request.Authorization.Scope.Correlation.ShouldBe(invocation);
        request.ExpectedOperation.ShouldBe(Operation());
    }

    [Fact]
    public void InputPromotionRequest_WhenAuthorizationUsesAnotherRun_ThrowsArgumentExceptionWithParamName()
    {
        var otherRun = new InRunOperationCorrelation(new OperationId(Guid.Parse("80000000-0000-0000-0000-000000000001")), new RunId(Guid.Parse("90000000-0000-0000-0000-000000000001")), Turn());
        var exception = ShouldThrowExactly<ArgumentException>(() => PromotionRequest(Authorization(Identity(), Agent(), Session(), otherRun)));
        exception.ParamName.ShouldBe("authorization");
    }

    [Fact]
    public void InputPromotionRequest_WhenBeforeFirstModelRequestHasPreviousTurn_ThrowsArgumentExceptionWithParamName()
    {
        var exception = ShouldThrowExactly<ArgumentException>(() => new InputPromotionRequest(Agent(), Session(), Lane(), Operation(), new OperationStateRevision(1), new SessionBranchCursor(Branch(), null), new SessionSequence(1), null, null, Identity(), Authorization(Identity(), Agent(), Session(), Operation()), PromotionBoundary.BeforeFirstModelRequest, Turn(), NextTurn(), 1));
        exception.ParamName.ShouldBe("previousTurnId");
    }

    private static ExecutionIdentity Identity() => TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
    private static ExecutionLaneId Lane() => new(Guid.Parse("40000000-0000-0000-0000-000000000001"));
    private static AgentId Agent() => new(Guid.Parse("20000000-0000-0000-0000-000000000001"));
    private static SessionId Session() => new(Guid.Parse("30000000-0000-0000-0000-000000000001"));
    private static BranchId Branch() => new(Guid.Parse("40000000-0000-0000-0000-000000000001"));
    private static TurnId Turn() => new(Guid.Parse("50000000-0000-0000-0000-000000000001"));
    private static TurnId NextTurn() => new(Guid.Parse("50000000-0000-0000-0000-000000000002"));
    private static InRunOperationCorrelation Operation() => new(new OperationId(Guid.Parse("60000000-0000-0000-0000-000000000001")), new RunId(Guid.Parse("70000000-0000-0000-0000-000000000001")), Turn());
    private static InputPromotionRequest PromotionRequest(SecurityAuthorizationContext authorization) => new(Agent(), Session(), Lane(), Operation(), new OperationStateRevision(1), new SessionBranchCursor(Branch(), null), new SessionSequence(1), null, null, Identity(), authorization, PromotionBoundary.AfterTurnCommitted, Turn(), NextTurn(), 1);
    private static SecurityAuthorizationContext Authorization(ExecutionIdentity identity, AgentId agentId, SessionId sessionId, OperationCorrelation correlation) => new(new SecurityProfileKey("profile"), new SecurityProfileVersion(1), new SecurityPolicySnapshotReference(new SecurityPolicySnapshotId(Guid.Parse("90000000-0000-0000-0000-000000000001")), new SecurityPolicyVersion(1), new ContentHash("safe")), new ComponentKey<ISecurityAuthority>("authority"), new AgentDefinitionRevision(0), new ConfigurationVersion(1), new SecurityAuthorizationScope(agentId, sessionId, correlation), identity);

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var version = new SessionVersion(1);
        var fence = new FencingToken(1);
        var request = new InputPromotionRequest(Agent(), Session(), Lane(), Operation(), new OperationStateRevision(1), new SessionBranchCursor(Branch(), null), new SessionSequence(1), version, fence, Identity(), Authorization(Identity(), Agent(), Session(), Operation()), PromotionBoundary.AfterTurnCommitted, Turn(), NextTurn(), 5);
        request.AgentId.ShouldBe(Agent());
        request.SessionId.ShouldBe(Session());
        request.ExecutionLaneId.ShouldBe(Lane());
        request.ExpectedOperation.ShouldBe(Operation());
        request.OperationStateRevision.ShouldBe(new OperationStateRevision(1));
        request.BranchCursor.ShouldBe(new SessionBranchCursor(Branch(), null));
        request.CutoffSequence.ShouldBe(new SessionSequence(1));
        request.ExpectedVersion.ShouldBe(version);
        request.ExpectedFencingToken.ShouldBe(fence);
        request.Identity.ShouldBe(Identity());
        request.Boundary.ShouldBe(PromotionBoundary.AfterTurnCommitted);
        request.PreviousTurnId.ShouldBe(Turn());
        request.TargetTurnId.ShouldBe(NextTurn());
        request.MaximumPromotions.ShouldBe(5);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new InputPromotionRequest(Agent(), Session(), Lane(), Operation(), new OperationStateRevision(1), new SessionBranchCursor(Branch(), null), new SessionSequence(1), null, null, Identity(), Authorization(Identity(), Agent(), Session(), Operation()), PromotionBoundary.AfterTurnCommitted, Turn(), NextTurn(), 1);
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static TException ShouldThrowExactly<TException>(Func<object?> action)
        where TException : Exception
    {
        var exception = Should.Throw<Exception>(() => _ = action());
        exception.GetType().ShouldBe(typeof(TException));
        return (TException) exception;
    }

    private static InputPromotionRequest MatrixPromotionRequest(string parameter)
    {
        var agentId = parameter == "agentId" ? default : Agent();
        var sessionId = parameter == "sessionId" ? default : Session();
        var laneId = parameter == "executionLaneId" ? default : Lane();
        var operation = parameter == "expectedOperation" ? null : Operation();
        var revision = parameter == "operationStateRevision" ? default : new OperationStateRevision(1);
        var cursor = parameter == "branchCursor" ? null : new SessionBranchCursor(Branch(), null);
        var fence = parameter == "expectedFencingToken" ? (FencingToken?) default(FencingToken) : null;
        var identity = parameter == "identity" ? null : Identity();
        var authorization = parameter == "authorization" ? null : Authorization(Identity(), Agent(), Session(), Operation());
        var boundary = parameter == "boundary" ? (PromotionBoundary) 42 : PromotionBoundary.AfterTurnCommitted;
        var targetTurnId = parameter == "targetTurnId" ? default : NextTurn();
        var maximumPromotions = parameter == "maximumPromotions" ? 0 : 1;
        return new InputPromotionRequest(agentId, sessionId, laneId, operation!, revision, cursor!, new SessionSequence(1), null, fence, identity!, authorization!, boundary, Turn(), targetTurnId, maximumPromotions);
    }

    public static IEnumerable<object?[]> InvalidConstructorCases()
    {
        yield return new object?[]
        {
            () => MatrixPromotionRequest("agentId"),
            typeof(ArgumentOutOfRangeException),
            "agentId"
        };
        yield return new object?[]
        {
            () => MatrixPromotionRequest("sessionId"),
            typeof(ArgumentOutOfRangeException),
            "sessionId"
        };
        yield return new object?[]
        {
            () => MatrixPromotionRequest("executionLaneId"),
            typeof(ArgumentOutOfRangeException),
            "executionLaneId"
        };
        yield return new object?[]
        {
            () => MatrixPromotionRequest("expectedOperation"),
            typeof(ArgumentNullException),
            "expectedOperation"
        };
        yield return new object?[]
        {
            () => MatrixPromotionRequest("operationStateRevision"),
            typeof(ArgumentOutOfRangeException),
            "operationStateRevision"
        };
        yield return new object?[]
        {
            () => MatrixPromotionRequest("branchCursor"),
            typeof(ArgumentNullException),
            "branchCursor"
        };
        yield return new object?[]
        {
            () => MatrixPromotionRequest("identity"),
            typeof(ArgumentNullException),
            "identity"
        };
        yield return new object?[]
        {
            () => MatrixPromotionRequest("authorization"),
            typeof(ArgumentNullException),
            "authorization"
        };
        yield return new object?[]
        {
            () => MatrixPromotionRequest("expectedFencingToken"),
            typeof(ArgumentOutOfRangeException),
            "expectedFencingToken"
        };
        yield return new object?[]
        {
            () => MatrixPromotionRequest("boundary"),
            typeof(ArgumentOutOfRangeException),
            "boundary"
        };
        yield return new object?[]
        {
            () => MatrixPromotionRequest("targetTurnId"),
            typeof(ArgumentOutOfRangeException),
            "targetTurnId"
        };
        yield return new object?[]
        {
            () => MatrixPromotionRequest("maximumPromotions"),
            typeof(ArgumentOutOfRangeException),
            "maximumPromotions"
        };
    }

    [Theory]
    [MemberData(nameof(InvalidConstructorCases))]
    public void Constructor_WhenDocumentedArgumentIsInvalid_ThrowsExactException(Func<InputPromotionRequest> construct, Type exceptionType, string parameterName)
    {
        var exception = Should.Throw<Exception>(() => _ = construct());
        exception.GetType().ShouldBe(exceptionType);
        ((ArgumentException) exception).ParamName.ShouldBe(parameterName);
    }
}
