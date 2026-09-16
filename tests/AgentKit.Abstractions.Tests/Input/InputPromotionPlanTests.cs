// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Input;



/// <summary>Verifies InputPromotionPlan behavior and contracts.</summary>
public sealed class InputPromotionPlanTests
{
    [Fact]
    public void InputPromotionPlanAndResults_WhenRequiredEvidenceIsNull_ThrowArgumentNullExceptionWithParamName()
    {
        var planException = ShouldThrowExactly<ArgumentNullException>(() => new InputPromotionPlan(null!));
        planException.ParamName.ShouldBe("snapshot");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsSnapshot()
    {
        var snapshot = Snapshot();
        var plan = new InputPromotionPlan(snapshot);
        plan.Snapshot.ShouldBeSameAs(snapshot);
        InputPromotionPlanningResult result = plan;
        _ = result.ShouldBeOfType<InputPromotionPlan>();
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new InputPromotionPlan(Snapshot());
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static InputPromotionSnapshot Snapshot() => new(Agent(), Session(), Lane(), Operation(), new OperationStateRevision(1), new SessionBranchCursor(Branch(), null), new SessionSequence(1), null, null, PromotionBoundary.AfterTurnCommitted, Turn(), NextTurn(), [new AdmissionId(Guid.Parse("00000000-0000-0000-0000-000000000001"))]);
    private static ExecutionLaneId Lane() => new(Guid.Parse("40000000-0000-0000-0000-000000000001"));
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
}
