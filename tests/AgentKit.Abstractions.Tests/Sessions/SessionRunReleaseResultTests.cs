// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

/// <summary>Verifies SessionRunReleaseResult derived behavior and contracts.</summary>
public sealed class SessionRunReleaseResultTests
{
    [Fact]
    public void SessionRunReleased_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var released = new SessionRunReleased(new SessionVersion(3), existing: true);
        released.NewVersion.ShouldBe(new SessionVersion(3));
        released.Existing.ShouldBeTrue();
    }

    [Fact]
    public void SessionRunReleased_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SessionRunReleased(new SessionVersion(1), existing: false);
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void SessionRunReleaseRejected_WhenKindIsUndefined_ThrowsExactArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new SessionRunReleaseRejected((SessionRunReleaseRejectionKind) 99, "reason")).ParamName.ShouldBe("kind");

    [Fact]
    public void SessionRunReleaseRejected_WhenSafeReasonIsBlank_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new SessionRunReleaseRejected(SessionRunReleaseRejectionKind.Fenced, " ")).ParamName.ShouldBe("safeReason");

    [Fact]
    public void SessionRunReleaseRejected_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SessionRunReleaseRejected(SessionRunReleaseRejectionKind.LaneNotFound, "rejected");
        var copy = original with { };
        copy.ShouldBe(original);
        original.Kind.ShouldBe(SessionRunReleaseRejectionKind.LaneNotFound);
        original.SafeReason.ShouldBe("rejected");
    }
}
