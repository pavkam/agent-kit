// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

/// <summary>Verifies SessionPendingInputsResult derived behavior and contracts.</summary>
public sealed class SessionPendingInputsResultTests
{
    [Fact]
    public void SessionPendingInputsLoaded_WhenExecutionLaneIdIsDefault_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new SessionPendingInputsLoaded(
            SessionsTestData.AgentId, SessionsTestData.SessionId, default, [], new SessionLaneRevision(1), SessionsTestData.Cursor()));
        exception.ParamName.ShouldBe("executionLaneId");
    }

    [Fact]
    public void SessionPendingInputsLoaded_WhenBranchCursorIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new SessionPendingInputsLoaded(
            SessionsTestData.AgentId, SessionsTestData.SessionId, SessionsTestData.LaneId, [], new SessionLaneRevision(1), null!));
        exception.ParamName.ShouldBe("branchCursor");
    }

    [Fact]
    public void SessionPendingInputsLoaded_WhenPendingContainsAlreadyPromotedInput_ThrowsExactArgumentException()
    {
        var promoted = new AdmittedInput(
            SessionsTestData.AdmissionId, SessionsTestData.AgentId, SessionsTestData.SessionId, SessionsTestData.LaneId,
            SessionsTestData.Identity(), new SessionSequence(1), SessionsTestData.Input(), SessionsTestData.Input(),
            SessionsTestData.Preprocessing(), DateTimeOffset.UnixEpoch, new SessionSequence(2));
        var exception = Should.Throw<ArgumentException>(() => new SessionPendingInputsLoaded(
            SessionsTestData.AgentId, SessionsTestData.SessionId, SessionsTestData.LaneId, [promoted],
            new SessionLaneRevision(1), SessionsTestData.Cursor()));
        exception.ParamName.ShouldBe("pending");
    }

    [Fact]
    public void SessionPendingInputsLoaded_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var pending = SessionsTestData.AdmittedInput();
        var revision = new SessionLaneRevision(3);
        var cursor = SessionsTestData.Cursor();
        var loaded = new SessionPendingInputsLoaded(
            SessionsTestData.AgentId, SessionsTestData.SessionId, SessionsTestData.LaneId, [pending], revision, cursor);

        loaded.AgentId.ShouldBe(SessionsTestData.AgentId);
        loaded.SessionId.ShouldBe(SessionsTestData.SessionId);
        loaded.ExecutionLaneId.ShouldBe(SessionsTestData.LaneId);
        loaded.Pending.ShouldBe([pending]);
        loaded.Revision.ShouldBe(revision);
        loaded.BranchCursor.ShouldBe(cursor);
    }

    [Fact]
    public void SessionPendingInputsLoaded_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SessionPendingInputsLoaded(
            SessionsTestData.AgentId, SessionsTestData.SessionId, SessionsTestData.LaneId, [],
            new SessionLaneRevision(1), SessionsTestData.Cursor());
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void SessionPendingInputsUnavailable_WhenSafeReasonIsBlank_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new SessionPendingInputsUnavailable(" ")).ParamName.ShouldBe("safeReason");

    [Fact]
    public void SessionPendingInputsUnavailable_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SessionPendingInputsUnavailable("unavailable");
        var copy = original with { };
        copy.ShouldBe(original);
        original.SafeReason.ShouldBe("unavailable");
    }
}
