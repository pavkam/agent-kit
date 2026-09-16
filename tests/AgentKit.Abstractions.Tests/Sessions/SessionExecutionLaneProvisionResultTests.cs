// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

/// <summary>Verifies SessionExecutionLaneProvisionResult behavior and contracts.</summary>
public sealed class SessionExecutionLaneProvisionResultTests
{
    [Fact]
    public void SessionExecutionLaneProvisioned_WhenExecutionLaneIdIsDefault_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => Provisioned(laneId: default(ExecutionLaneId)));
        exception.ParamName.ShouldBe("executionLaneId");
    }

    [Fact]
    public void SessionExecutionLaneProvisioned_WhenBranchCursorIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new SessionExecutionLaneProvisioned(SessionsTestData.LaneId, null!, new SessionLaneRevision(1), new SessionVersion(2), true));
        exception.ParamName.ShouldBe("branchCursor");
    }

    [Fact]
    public void SessionExecutionLaneProvisioned_WhenLaneRevisionIsDefault_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => Provisioned(laneRevision: default(SessionLaneRevision)));
        exception.ParamName.ShouldBe("laneRevision");
    }

    [Fact]
    public void SessionExecutionLaneProvisioned_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var provisioned = Provisioned();
        provisioned.ExecutionLaneId.ShouldBe(SessionsTestData.LaneId);
        provisioned.SessionVersion.ShouldBe(new SessionVersion(2));
        provisioned.Existing.ShouldBeTrue();
    }

    [Fact]
    public void SessionExecutionLaneProvisioned_With_WhenApplied_ProducesEqualCopy()
    {
        var original = Provisioned();
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static SessionExecutionLaneProvisioned Provisioned(ExecutionLaneId? laneId = null, SessionLaneRevision? laneRevision = null) =>
        new(laneId ?? SessionsTestData.LaneId, SessionsTestData.Cursor(), laneRevision ?? new SessionLaneRevision(1), new SessionVersion(2), existing: true);
}
