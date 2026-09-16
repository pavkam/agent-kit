// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Input;



/// <summary>Verifies InputPromotionContext behavior and contracts.</summary>
public sealed class InputPromotionContextTests
{
    [Fact]
    public void InputPromotionContext_WhenEligibleInputTargetsAnotherLane_ThrowsArgumentExceptionWithParamName()
    {
        var payload = Payload(1, InputDelivery.Steer);
        var input = new AdmittedInput(Admission(1), Agent(), Session(), OtherLane(), Identity(), new SessionSequence(1), payload, payload, Manifest(), DateTimeOffset.UnixEpoch);
        var exception = ShouldThrowExactly<ArgumentException>(() => new InputPromotionContext(Agent(), Session(), Lane(), Operation(), new OperationStateRevision(1), new SessionBranchCursor(Branch(), null), new SessionSequence(1), null, null, PromotionBoundary.AfterTurnCommitted, Turn(), NextTurn(), [input], 1));
        exception.ParamName.ShouldBe("eligible");
    }

    [Fact]
    public void InputPromotionContext_WhenEligibleInputsAreDuplicatePromotedOrPostCutoff_RejectsBeforePolicyUse()
    {
        var payload = Payload(1, InputDelivery.Steer);
        var pending = Admitted(payload, payload);
        var promoted = Admitted(payload, payload, new SessionSequence(2));
        var duplicateException = ShouldThrowExactly<ArgumentException>(() => PromotionContext([pending, pending]));
        var promotedException = ShouldThrowExactly<ArgumentException>(() => PromotionContext([promoted]));
        var cutoffException = ShouldThrowExactly<ArgumentException>(() => PromotionContext([pending], new SessionSequence(0)));
        duplicateException.ParamName.ShouldBe("eligible");
        promotedException.ParamName.ShouldBe("eligible");
        cutoffException.ParamName.ShouldBe("eligible");
    }

    [Fact]
    public void InputPromotionContext_WhenCheckpointRetainsTargetTurn_PreservesTheValidShape()
    {
        var context = new InputPromotionContext(Agent(), Session(), Lane(), Operation(), new OperationStateRevision(1), new SessionBranchCursor(Branch(), null), new SessionSequence(1), null, null, PromotionBoundary.AfterContinuationCheckpoint, Turn(), Turn(), [], 1);
        context.PreviousTurnId.ShouldBe(Turn());
        context.TargetTurnId.ShouldBe(Turn());
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var version = new SessionVersion(1);
        var fence = new FencingToken(1);
        var payload = Payload(1, InputDelivery.Steer);
        var eligible = Admitted(payload, payload);
        var context = new InputPromotionContext(Agent(), Session(), Lane(), Operation(), new OperationStateRevision(1), new SessionBranchCursor(Branch(), null), new SessionSequence(1), version, fence, PromotionBoundary.AfterTurnCommitted, Turn(), NextTurn(), [eligible], 2);
        context.AgentId.ShouldBe(Agent());
        context.SessionId.ShouldBe(Session());
        context.ExecutionLaneId.ShouldBe(Lane());
        context.ExpectedOperation.ShouldBe(Operation());
        context.OperationStateRevision.ShouldBe(new OperationStateRevision(1));
        context.BranchCursor.ShouldBe(new SessionBranchCursor(Branch(), null));
        context.CutoffSequence.ShouldBe(new SessionSequence(1));
        context.ExpectedVersion.ShouldBe(version);
        context.ExpectedFencingToken.ShouldBe(fence);
        context.Boundary.ShouldBe(PromotionBoundary.AfterTurnCommitted);
        context.Eligible.ShouldBe([eligible]);
        context.MaximumPromotions.ShouldBe(2);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = PromotionContext([]);
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static AdmittedInput Admitted(AgentInput original, AgentInput effective, SessionSequence? promoted = null) => new(Admission(1), Agent(), Session(), Lane(), Identity(), new SessionSequence(1), original, effective, Manifest(), DateTimeOffset.UnixEpoch, promoted);
    private static AgentInput Payload(long id, InputDelivery delivery) => new(Input(id), delivery, [Part()], ExtensionData.Empty);
    private static TextPart Part() => new("input", TextSemantics.Plain, ExtensionData.Empty);
    private static InputPreprocessingManifest Manifest() => new(new ConfigurationVersion(1), new InputFingerprint("original:1"), new InputFingerprint("effective:1"));
    private static ExecutionIdentity Identity() => TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
    private static ExecutionLaneId Lane() => new(Guid.Parse("40000000-0000-0000-0000-000000000001"));
    private static ExecutionLaneId OtherLane() => new(Guid.Parse("40000000-0000-0000-0000-000000000002"));
    private static AdmissionId Admission(long value) => new(Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}"));
    private static InputId Input(long value) => new(Guid.Parse($"10000000-0000-0000-0000-{value:000000000000}"));
    private static AgentId Agent() => new(Guid.Parse("20000000-0000-0000-0000-000000000001"));
    private static SessionId Session() => new(Guid.Parse("30000000-0000-0000-0000-000000000001"));
    private static BranchId Branch() => new(Guid.Parse("40000000-0000-0000-0000-000000000001"));
    private static TurnId Turn() => new(Guid.Parse("50000000-0000-0000-0000-000000000001"));
    private static TurnId NextTurn() => new(Guid.Parse("50000000-0000-0000-0000-000000000002"));
    private static InRunOperationCorrelation Operation() => new(new OperationId(Guid.Parse("60000000-0000-0000-0000-000000000001")), new RunId(Guid.Parse("70000000-0000-0000-0000-000000000001")), Turn());
    private static InputPromotionContext PromotionContext(ImmutableArray<AdmittedInput> eligible, SessionSequence? cutoff = null) => new(Agent(), Session(), Lane(), Operation(), new OperationStateRevision(1), new SessionBranchCursor(Branch(), null), cutoff ?? new SessionSequence(1), null, null, PromotionBoundary.AfterTurnCommitted, Turn(), NextTurn(), eligible, 2);

    private static TException ShouldThrowExactly<TException>(Func<object?> action)
        where TException : Exception
    {
        var exception = Should.Throw<Exception>(() => _ = action());
        exception.GetType().ShouldBe(typeof(TException));
        return (TException) exception;
    }

    private static InputPromotionContext MatrixPromotionContext(string parameter)
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
        var maximumPromotions = parameter == "maximumPromotions" ? 0 : 1;
        return new InputPromotionContext(agentId, sessionId, laneId, operation!, revision, cursor!, new SessionSequence(1), null, fence, boundary, Turn(), targetTurnId, [], maximumPromotions);
    }

    public static IEnumerable<object?[]> InvalidConstructorCases()
    {
        yield return new object?[]
        {
            () => MatrixPromotionContext("agentId"),
            typeof(ArgumentOutOfRangeException),
            "agentId"
        };
        yield return new object?[]
        {
            () => MatrixPromotionContext("sessionId"),
            typeof(ArgumentOutOfRangeException),
            "sessionId"
        };
        yield return new object?[]
        {
            () => MatrixPromotionContext("executionLaneId"),
            typeof(ArgumentOutOfRangeException),
            "executionLaneId"
        };
        yield return new object?[]
        {
            () => MatrixPromotionContext("expectedOperation"),
            typeof(ArgumentNullException),
            "expectedOperation"
        };
        yield return new object?[]
        {
            () => MatrixPromotionContext("operationStateRevision"),
            typeof(ArgumentOutOfRangeException),
            "operationStateRevision"
        };
        yield return new object?[]
        {
            () => MatrixPromotionContext("branchCursor"),
            typeof(ArgumentNullException),
            "branchCursor"
        };
        yield return new object?[]
        {
            () => MatrixPromotionContext("expectedFencingToken"),
            typeof(ArgumentOutOfRangeException),
            "expectedFencingToken"
        };
        yield return new object?[]
        {
            () => MatrixPromotionContext("boundary"),
            typeof(ArgumentOutOfRangeException),
            "boundary"
        };
        yield return new object?[]
        {
            () => MatrixPromotionContext("targetTurnId"),
            typeof(ArgumentOutOfRangeException),
            "targetTurnId"
        };
        yield return new object?[]
        {
            () => MatrixPromotionContext("maximumPromotions"),
            typeof(ArgumentOutOfRangeException),
            "maximumPromotions"
        };
    }

    [Theory]
    [MemberData(nameof(InvalidConstructorCases))]
    public void Constructor_WhenDocumentedArgumentIsInvalid_ThrowsExactException(Func<InputPromotionContext> construct, Type exceptionType, string parameterName)
    {
        var exception = Should.Throw<Exception>(() => _ = construct());
        exception.GetType().ShouldBe(exceptionType);
        ((ArgumentException) exception).ParamName.ShouldBe(parameterName);
    }
}
