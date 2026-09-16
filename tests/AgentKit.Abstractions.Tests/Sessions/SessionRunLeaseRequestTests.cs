// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

/// <summary>Verifies SessionRunLeaseRequest behavior and contracts.</summary>
public sealed class SessionRunLeaseRequestTests
{
    [Fact]
    public void Constructor_WhenContextIsNotInRun_ThrowsExactArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new SessionRunLeaseRequest(SessionsTestData.BeforeRunContext(), new OperationStateRevision(1)));
        exception.ParamName.ShouldBe("context");
    }

    [Fact]
    public void Constructor_WhenExpectedStateRevisionIsDefault_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new SessionRunLeaseRequest(SessionsTestData.InRunContext(), default));
        exception.ParamName.ShouldBe("expectedStateRevision");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_ExposesDerivedProperties()
    {
        var context = SessionsTestData.InRunContext();
        var request = new SessionRunLeaseRequest(context, new OperationStateRevision(1));
        request.AgentId.ShouldBe(context.AgentId);
        request.SessionId.ShouldBe(context.SessionId);
        request.ExecutionLaneId.ShouldBe(context.ExecutionLaneId!.Value);
        request.OperationId.ShouldBe(SessionsTestData.OperationId);
        request.RunId.ShouldBe(SessionsTestData.RunId);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SessionRunLeaseRequest(SessionsTestData.InRunContext(), new OperationStateRevision(1));
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
