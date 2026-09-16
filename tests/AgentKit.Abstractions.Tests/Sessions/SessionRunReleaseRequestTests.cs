// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

/// <summary>Verifies SessionRunReleaseRequest behavior and contracts.</summary>
public sealed class SessionRunReleaseRequestTests
{
    [Fact]
    public void Constructor_WhenContextIsNotInRun_ThrowsExactArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() =>
            new SessionRunReleaseRequest(SessionsTestData.BeforeRunContext(), new OperationStateRevision(1), new SessionVersion(2), new IdempotencyKey("release")));
        exception.ParamName.ShouldBe("context");
    }

    [Fact]
    public void Constructor_WhenExpectedStateRevisionIsDefault_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() =>
            new SessionRunReleaseRequest(SessionsTestData.InRunContext(), default, new SessionVersion(2), new IdempotencyKey("release")));
        exception.ParamName.ShouldBe("expectedStateRevision");
    }

    [Fact]
    public void Constructor_WhenIdempotencyKeyIsBlank_ThrowsExactArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() =>
            new SessionRunReleaseRequest(SessionsTestData.InRunContext(), new OperationStateRevision(1), new SessionVersion(2), default));
        exception.ParamName.ShouldBe("idempotencyKey");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_ExposesDerivedProperties()
    {
        var context = SessionsTestData.InRunContext();
        var request = Request(context, new OperationStateRevision(1), new IdempotencyKey("release"));
        request.AgentId.ShouldBe(context.AgentId);
        request.SessionId.ShouldBe(context.SessionId);
        request.ExecutionLaneId.ShouldBe(context.ExecutionLaneId!.Value);
        request.OperationId.ShouldBe(SessionsTestData.OperationId);
        request.RunId.ShouldBe(SessionsTestData.RunId);
        request.ExpectedVersion.ShouldBe(new SessionVersion(2));
        request.IdempotencyKey.ShouldBe(new IdempotencyKey("release"));
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = Request(SessionsTestData.InRunContext(), new OperationStateRevision(1), new IdempotencyKey("release"));
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static SessionRunReleaseRequest Request(SessionOperationContext context, OperationStateRevision expectedStateRevision, IdempotencyKey idempotencyKey) =>
        new(context, expectedStateRevision, new SessionVersion(2), idempotencyKey);
}
