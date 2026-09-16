// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

/// <summary>Verifies ExecutionLaneProvisionedSessionEntry behavior and contracts.</summary>
public sealed class ExecutionLaneProvisionedSessionEntryTests
{
    [Fact]
    public void Constructor_WhenExecutionLaneIdIsDefault_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => Entry(laneId: default(ExecutionLaneId)));
        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe("executionLaneId");
    }

    [Fact]
    public void Constructor_WhenLaneRevisionIsDefault_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => Entry(laneRevision: default(SessionLaneRevision)));
        exception.ParamName.ShouldBe("laneRevision");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var entry = Entry();
        entry.ExecutionLaneId.ShouldBe(SessionsTestData.LaneId);
        entry.LaneRevision.ShouldBe(new SessionLaneRevision(1));
        entry.SessionProfile.ShouldBe(SessionsTestData.ProfileReference());
        entry.Configuration.ShouldBe(SessionsTestData.Configuration());
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = Entry();
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static ExecutionLaneProvisionedSessionEntry Entry(ExecutionLaneId? laneId = null, SessionLaneRevision? laneRevision = null) =>
        new(SessionsTestData.EntryId, SessionsTestData.Address(), SessionsTestData.BeforeRun(), SessionsTestData.BranchId,
            new SessionSequence(1), null, DateTimeOffset.UnixEpoch, new SchemaVersion("1"),
            laneId ?? SessionsTestData.LaneId, laneRevision ?? new SessionLaneRevision(1),
            SessionsTestData.ProfileReference(), SessionsTestData.Configuration());
}
