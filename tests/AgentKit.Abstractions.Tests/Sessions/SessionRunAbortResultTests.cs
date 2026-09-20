// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

/// <summary>Verifies SessionRunAbortResult derived behavior and contracts.</summary>
public sealed class SessionRunAbortResultTests
{
    [Fact]
    public void SessionRunAbortRecorded_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var recorded = new SessionRunAbortRecorded(
            new SessionVersion(4), new SessionLaneRevision(5), new OperationStateRevision(6), existing: true);
        recorded.NewVersion.ShouldBe(new SessionVersion(4));
        recorded.LaneRevision.ShouldBe(new SessionLaneRevision(5));
        recorded.StateRevision.ShouldBe(new OperationStateRevision(6));
        recorded.Existing.ShouldBeTrue();
    }

    [Fact]
    public void SessionRunAbortRecorded_WhenLaneRevisionIsDefault_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() =>
            new SessionRunAbortRecorded(new SessionVersion(1), default, new OperationStateRevision(2), existing: false));
        exception.ParamName.ShouldBe("laneRevision");
    }

    [Fact]
    public void SessionRunAbortRecorded_WhenStateRevisionIsDefault_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() =>
            new SessionRunAbortRecorded(new SessionVersion(1), new SessionLaneRevision(2), default, existing: false));
        exception.ParamName.ShouldBe("stateRevision");
    }

    [Fact]
    public void SessionRunAbortRejected_WhenKindIsUndefined_ThrowsExactArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() =>
            new SessionRunAbortRejected((SessionRunAbortRejectionKind) 99, "reason")).ParamName.ShouldBe("kind");

    [Fact]
    public void SessionRunAbortRejected_WhenSafeReasonIsBlank_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() =>
            new SessionRunAbortRejected(SessionRunAbortRejectionKind.Fenced, " ")).ParamName.ShouldBe("safeReason");
}
