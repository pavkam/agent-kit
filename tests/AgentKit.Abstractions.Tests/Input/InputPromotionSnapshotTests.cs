// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Input;



/// <summary>Verifies InputPromotionSnapshot behavior and contracts.</summary>
public sealed class InputPromotionSnapshotTests
{
    [Fact]
    public void InputPromotionSnapshot_WhenAdmissionsRepeat_ThrowsArgumentExceptionWithParamName()
    {
        var admission = Admission(1);
        var exception = ShouldThrowExactly<ArgumentException>(() => Snapshot([admission, admission]));
        exception.ParamName.ShouldBe("admissionIds");
    }

    [Fact]
    public void InputPromotionSnapshot_WhenPreviousTurnDoesNotMatchBoundary_ThrowsArgumentExceptionWithParamName()
    {
        var afterTurnException = ShouldThrowExactly<ArgumentException>(() => new InputPromotionSnapshot(Agent(), Session(), Lane(), Operation(), new OperationStateRevision(1), new SessionBranchCursor(Branch(), null), new SessionSequence(1), null, null, PromotionBoundary.AfterTurnCommitted, null, NextTurn(), []));
        var firstRequestException = ShouldThrowExactly<ArgumentException>(() => new InputPromotionSnapshot(Agent(), Session(), Lane(), Operation(), new OperationStateRevision(1), new SessionBranchCursor(Branch(), null), new SessionSequence(1), null, null, PromotionBoundary.BeforeFirstModelRequest, Turn(), NextTurn(), []));
        afterTurnException.ParamName.ShouldBe("previousTurnId");
        firstRequestException.ParamName.ShouldBe("previousTurnId");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsOptionalEvidence()
    {
        var version = new SessionVersion(1);
        var fence = new FencingToken(1);
        var snapshot = new InputPromotionSnapshot(Agent(), Session(), Lane(), Operation(), new OperationStateRevision(1), new SessionBranchCursor(Branch(), null), new SessionSequence(1), version, fence, PromotionBoundary.AfterTurnCommitted, Turn(), NextTurn(), [Admission(1)]);
        snapshot.ExpectedVersion.ShouldBe(version);
        snapshot.ExpectedFencingToken.ShouldBe(fence);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = Snapshot([Admission(1)]);
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static InputPromotionSnapshot Snapshot(ImmutableArray<AdmissionId> admissions) => new(Agent(), Session(), Lane(), Operation(), new OperationStateRevision(1), new SessionBranchCursor(Branch(), null), new SessionSequence(1), null, null, PromotionBoundary.AfterTurnCommitted, Turn(), NextTurn(), admissions);
    private static ExecutionLaneId Lane() => new(Guid.Parse("40000000-0000-0000-0000-000000000001"));
    private static AdmissionId Admission(long value) => new(Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}"));
    private static AgentId Agent() => new(Guid.Parse("20000000-0000-0000-0000-000000000001"));
    private static SessionId Session() => new(Guid.Parse("30000000-0000-0000-0000-000000000001"));
    private static BranchId Branch() => new(Guid.Parse("40000000-0000-0000-0000-000000000001"));
    private static TurnId Turn() => new(Guid.Parse("50000000-0000-0000-0000-000000000001"));
    private static TurnId NextTurn() => new(Guid.Parse("50000000-0000-0000-0000-000000000002"));
    private static InRunOperationCorrelation Operation() => new(new OperationId(Guid.Parse("60000000-0000-0000-0000-000000000001")), new RunId(Guid.Parse("70000000-0000-0000-0000-000000000001")), Turn());

    private static TException ShouldThrowExactly<TException>(Func<object?> action)
        where TException : Exception
    {
        var exception = Should.Throw<Exception>(() => _ = action());
        exception.GetType().ShouldBe(typeof(TException));
        return (TException) exception;
    }

    private static InputPromotionSnapshot MatrixSnapshot(string parameter)
    {
        var agentId = parameter == "agentId" ? default : Agent();
        var sessionId = parameter == "sessionId" ? default : Session();
        var laneId = parameter == "executionLaneId" ? default : Lane();
        var operation = parameter == "expectedOperation" ? null : Operation();
        var revision = parameter == "operationStateRevision" ? default : new OperationStateRevision(1);
        var cursor = parameter == "branchCursor" ? null : new SessionBranchCursor(Branch(), null);
        var fence = parameter == "expectedFencingToken" ? (FencingToken?) default(FencingToken) : null;
        var boundary = parameter == "boundary" ? (PromotionBoundary) 42 : PromotionBoundary.AfterTurnCommitted;
        var targetTurnId = parameter == "targetTurnId" ? default : NextTurn();
        return new InputPromotionSnapshot(agentId, sessionId, laneId, operation!, revision, cursor!, new SessionSequence(1), null, fence, boundary, Turn(), targetTurnId, []);
    }

    public static IEnumerable<object?[]> InvalidConstructorCases()
    {
        yield return new object?[]
        {
            () => MatrixSnapshot("agentId"),
            typeof(ArgumentOutOfRangeException),
            "agentId"
        };
        yield return new object?[]
        {
            () => MatrixSnapshot("sessionId"),
            typeof(ArgumentOutOfRangeException),
            "sessionId"
        };
        yield return new object?[]
        {
            () => MatrixSnapshot("executionLaneId"),
            typeof(ArgumentOutOfRangeException),
            "executionLaneId"
        };
        yield return new object?[]
        {
            () => MatrixSnapshot("expectedOperation"),
            typeof(ArgumentNullException),
            "expectedOperation"
        };
        yield return new object?[]
        {
            () => MatrixSnapshot("operationStateRevision"),
            typeof(ArgumentOutOfRangeException),
            "operationStateRevision"
        };
        yield return new object?[]
        {
            () => MatrixSnapshot("branchCursor"),
            typeof(ArgumentNullException),
            "branchCursor"
        };
        yield return new object?[]
        {
            () => MatrixSnapshot("expectedFencingToken"),
            typeof(ArgumentOutOfRangeException),
            "expectedFencingToken"
        };
        yield return new object?[]
        {
            () => MatrixSnapshot("boundary"),
            typeof(ArgumentOutOfRangeException),
            "boundary"
        };
        yield return new object?[]
        {
            () => MatrixSnapshot("targetTurnId"),
            typeof(ArgumentOutOfRangeException),
            "targetTurnId"
        };
    }

    [Theory]
    [MemberData(nameof(InvalidConstructorCases))]
    public void Constructor_WhenDocumentedArgumentIsInvalid_ThrowsExactException(Func<InputPromotionSnapshot> construct, Type exceptionType, string parameterName)
    {
        var exception = Should.Throw<Exception>(() => _ = construct());
        exception.GetType().ShouldBe(exceptionType);
        ((ArgumentException) exception).ParamName.ShouldBe(parameterName);
    }
}
