// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

/// <summary>Verifies SessionLaneState behavior and contracts.</summary>
public sealed class SessionLaneStateTests
{
    [Fact]
    public void Constructor_WhenExecutionLaneIdIsDefault_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new SessionLaneState(default, new SessionLaneRevision(1), SessionsTestData.Cursor(), null));
        exception.ParamName.ShouldBe("executionLaneId");
    }

    [Fact]
    public void Constructor_WhenRevisionIsDefault_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new SessionLaneState(SessionsTestData.LaneId, default, SessionsTestData.Cursor(), null));
        exception.ParamName.ShouldBe("revision");
    }

    [Fact]
    public void Constructor_WhenBranchCursorIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new SessionLaneState(SessionsTestData.LaneId, new SessionLaneRevision(1), null!, null));
        exception.ParamName.ShouldBe("branchCursor");
    }

    [Fact]
    public void Constructor_WhenAcceptedStateLaneDiffers_ThrowsExactArgumentException()
    {
        var accepted = SessionsTestData.AcceptedRunState();
        var otherLaneId = new ExecutionLaneId(Guid.Parse("b0000000-0000-0000-0000-000000000001"));
        var exception = Should.Throw<ArgumentException>(() => new SessionLaneState(
            otherLaneId, accepted.LaneRevision, accepted.CommittedCursor, accepted));
        exception.ParamName.ShouldBe("acceptedState");
    }

    [Fact]
    public void Constructor_WhenLiveRevisionExceedsAcceptedStateRevision_RoundTripsBothValues()
    {
        // A run's acceptance-time SessionAcceptedRunState.LaneRevision/CommittedCursor stay fixed while the lane
        // remains accepted; subsequent admission or mid-run promotion still advances the lane's own live revision
        // and cursor independently, so the two are no longer required to match.
        var accepted = SessionsTestData.AcceptedRunState();
        var advancedRevision = new SessionLaneRevision(accepted.LaneRevision.Value + 1);
        var advancedCursor = new SessionBranchCursor(
            SessionsTestData.BranchId, new SessionEntryId(Guid.Parse("b0000000-0000-0000-0000-000000000002")));

        var state = new SessionLaneState(accepted.ExecutionLaneId, advancedRevision, advancedCursor, accepted);

        state.Revision.ShouldBe(advancedRevision);
        state.BranchCursor.ShouldBe(advancedCursor);
        state.AcceptedState.ShouldBe(accepted);
    }

    [Fact]
    public void Constructor_WhenIdleWithNoAcceptedState_RoundTripsProperties()
    {
        var cursor = SessionsTestData.Cursor();
        var state = new SessionLaneState(SessionsTestData.LaneId, new SessionLaneRevision(1), cursor, null);

        state.ExecutionLaneId.ShouldBe(SessionsTestData.LaneId);
        state.Revision.ShouldBe(new SessionLaneRevision(1));
        state.BranchCursor.ShouldBe(cursor);
        state.AcceptedState.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WhenAcceptedStateIsConsistent_RoundTripsProperties()
    {
        var accepted = SessionsTestData.AcceptedRunState();
        var state = new SessionLaneState(accepted.ExecutionLaneId, accepted.LaneRevision, accepted.CommittedCursor, accepted);

        state.AcceptedState.ShouldBe(accepted);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SessionLaneState(SessionsTestData.LaneId, new SessionLaneRevision(1), SessionsTestData.Cursor(), null);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
