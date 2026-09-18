// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.AgentLoop;

/// <summary>Verifies LoopLaneAdmission behavior and contracts.</summary>
public sealed class LoopLaneAdmissionTests
{
    [Fact]
    public void Constructor_WhenExecutionLaneIdIsDefault_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new LoopLaneAdmission(
            default, LoopTestData.InRun(), new OperationStateRevision(1)));
        exception.ParamName.ShouldBe("executionLaneId");
    }

    [Fact]
    public void Constructor_WhenAcceptedCorrelationIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new LoopLaneAdmission(
            LoopTestData.ExecutionLaneId, null!, new OperationStateRevision(1)));
        exception.ParamName.ShouldBe("acceptedCorrelation");
    }

    [Fact]
    public void Constructor_WhenAcceptedCorrelationCarriesNoTurnId_ThrowsExactArgumentException()
    {
        var correlation = new InRunOperationCorrelation(LoopTestData.OperationId, LoopTestData.RunId, null);
        var exception = Should.Throw<ArgumentException>(() => new LoopLaneAdmission(
            LoopTestData.ExecutionLaneId, correlation, new OperationStateRevision(1)));
        exception.ParamName.ShouldBe("acceptedCorrelation");
    }

    [Fact]
    public void Constructor_WhenOperationStateRevisionIsDefault_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new LoopLaneAdmission(
            LoopTestData.ExecutionLaneId, LoopTestData.InRun(), default));
        exception.ParamName.ShouldBe("operationStateRevision");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var correlation = LoopTestData.InRun();
        var revision = new OperationStateRevision(3);
        var admission = new LoopLaneAdmission(LoopTestData.ExecutionLaneId, correlation, revision);

        admission.ExecutionLaneId.ShouldBe(LoopTestData.ExecutionLaneId);
        admission.AcceptedCorrelation.ShouldBe(correlation);
        admission.OperationStateRevision.ShouldBe(revision);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new LoopLaneAdmission(LoopTestData.ExecutionLaneId, LoopTestData.InRun(), new OperationStateRevision(1));
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
