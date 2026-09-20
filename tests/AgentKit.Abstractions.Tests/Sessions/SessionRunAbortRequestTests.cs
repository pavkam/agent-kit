// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

/// <summary>Verifies SessionRunAbortRequest behavior and contracts.</summary>
public sealed class SessionRunAbortRequestTests
{
    [Fact]
    public void Constructor_WhenContextIsNotInRun_ThrowsExactArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() =>
            new SessionRunAbortRequest(
                SessionsTestData.BeforeRunContext(), new OperationStateRevision(1), new SessionLaneRevision(2),
                new SessionVersion(3), new IdempotencyKey("abort")));
        exception.ParamName.ShouldBe("context");
    }

    [Fact]
    public void Constructor_WhenExpectedStateRevisionIsDefault_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() =>
            new SessionRunAbortRequest(
                SessionsTestData.InRunContext(), default, new SessionLaneRevision(2), new SessionVersion(3),
                new IdempotencyKey("abort")));
        exception.ParamName.ShouldBe("expectedStateRevision");
    }

    [Fact]
    public void Constructor_WhenExpectedLaneRevisionIsDefault_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() =>
            new SessionRunAbortRequest(
                SessionsTestData.InRunContext(), new OperationStateRevision(1), default, new SessionVersion(3),
                new IdempotencyKey("abort")));
        exception.ParamName.ShouldBe("expectedLaneRevision");
    }

    [Fact]
    public void Constructor_WhenIdempotencyKeyIsBlank_ThrowsExactArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() =>
            new SessionRunAbortRequest(
                SessionsTestData.InRunContext(), new OperationStateRevision(1), new SessionLaneRevision(2),
                new SessionVersion(3), default));
        exception.ParamName.ShouldBe("idempotencyKey");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_ExposesDerivedProperties()
    {
        var context = SessionsTestData.InRunContext();
        var request = new SessionRunAbortRequest(
            context, new OperationStateRevision(1), new SessionLaneRevision(2), new SessionVersion(3),
            new IdempotencyKey("abort"));
        request.AgentId.ShouldBe(context.AgentId);
        request.SessionId.ShouldBe(context.SessionId);
        request.ExecutionLaneId.ShouldBe(context.ExecutionLaneId!.Value);
        request.OperationId.ShouldBe(SessionsTestData.OperationId);
        request.RunId.ShouldBe(SessionsTestData.RunId);
        request.ExpectedLaneRevision.ShouldBe(new SessionLaneRevision(2));
        request.ExpectedVersion.ShouldBe(new SessionVersion(3));
        request.IdempotencyKey.ShouldBe(new IdempotencyKey("abort"));
    }
}
