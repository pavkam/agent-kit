// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

/// <summary>Verifies SessionLaneStateResult derived behavior and contracts.</summary>
public sealed class SessionLaneStateResultTests
{
    [Fact]
    public void SessionLaneStateLoaded_WhenStateIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new SessionLaneStateLoaded(null!));
        exception.ParamName.ShouldBe("state");
    }

    [Fact]
    public void SessionLaneStateLoaded_With_WhenApplied_ProducesEqualCopy()
    {
        var state = new SessionLaneState(SessionsTestData.LaneId, new SessionLaneRevision(1), SessionsTestData.Cursor(), null);
        var original = new SessionLaneStateLoaded(state);
        var copy = original with { };
        copy.ShouldBe(original);
        original.State.ShouldBe(state);
    }

    [Fact]
    public void SessionLaneStateNotProvisioned_WhenSafeReasonIsBlank_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new SessionLaneStateNotProvisioned(" ")).ParamName.ShouldBe("safeReason");

    [Fact]
    public void SessionLaneStateNotProvisioned_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SessionLaneStateNotProvisioned("not provisioned");
        var copy = original with { };
        copy.ShouldBe(original);
        original.SafeReason.ShouldBe("not provisioned");
    }

    [Fact]
    public void SessionLaneStateUnavailable_WhenSafeReasonIsBlank_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new SessionLaneStateUnavailable(" ")).ParamName.ShouldBe("safeReason");

    [Fact]
    public void SessionLaneStateUnavailable_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SessionLaneStateUnavailable("unavailable");
        var copy = original with { };
        copy.ShouldBe(original);
        original.SafeReason.ShouldBe("unavailable");
    }
}
