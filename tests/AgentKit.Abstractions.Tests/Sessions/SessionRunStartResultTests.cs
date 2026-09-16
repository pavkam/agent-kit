// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

/// <summary>Verifies SessionRunStartResult derived behavior and contracts.</summary>
public sealed class SessionRunStartResultTests
{
    [Fact]
    public void SessionRunStartBusy_WhenOperationIdIsDefault_ThrowsExactArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new SessionRunStartBusy(default, new RunId(Guid.NewGuid()))).ParamName.ShouldBe("operationId");

    [Fact]
    public void SessionRunStartBusy_WhenRunIdIsDefault_ThrowsExactArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new SessionRunStartBusy(new OperationId(Guid.NewGuid()), default)).ParamName.ShouldBe("runId");

    [Fact]
    public void SessionRunStartBusy_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SessionRunStartBusy(new OperationId(Guid.NewGuid()), new RunId(Guid.NewGuid()));
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void SessionRunStartBusy_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var operationId = new OperationId(Guid.NewGuid());
        var runId = new RunId(Guid.NewGuid());
        var busy = new SessionRunStartBusy(operationId, runId);
        busy.OperationId.ShouldBe(operationId);
        busy.RunId.ShouldBe(runId);
    }

    [Fact]
    public void SessionRunStartConflict_WhenKindIsUndefined_ThrowsExactArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new SessionRunStartConflict((SessionRunStartConflictKind) 99, "reason")).ParamName.ShouldBe("kind");

    [Fact]
    public void SessionRunStartConflict_WhenSafeReasonIsBlank_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new SessionRunStartConflict(SessionRunStartConflictKind.Idempotency, " ")).ParamName.ShouldBe("safeReason");

    [Fact]
    public void SessionRunStartConflict_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SessionRunStartConflict(SessionRunStartConflictKind.BranchCursor, "conflict");
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void SessionRunStartConflict_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var conflict = new SessionRunStartConflict(SessionRunStartConflictKind.SessionVersion, "conflict");
        conflict.Kind.ShouldBe(SessionRunStartConflictKind.SessionVersion);
        conflict.SafeReason.ShouldBe("conflict");
    }

    [Fact]
    public void SessionRunStateLoaded_WhenStateIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new SessionRunStateLoaded(null!)).ParamName.ShouldBe("state");

    [Fact]
    public void SessionRunAccepted_WhenStateIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new SessionRunAccepted(null!, new SessionVersion(1), existing: false)).ParamName.ShouldBe("state");

    [Fact]
    public void SessionRunAccepted_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var state = SessionsTestData.AcceptedRunState();
        var accepted = new SessionRunAccepted(state, new SessionVersion(3), existing: true);
        accepted.State.ShouldBe(state);
        accepted.SessionVersion.ShouldBe(new SessionVersion(3));
        accepted.Existing.ShouldBeTrue();
    }

    [Fact]
    public void SessionRunAccepted_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SessionRunAccepted(SessionsTestData.AcceptedRunState(), new SessionVersion(3), existing: true);
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void SessionRunStartFenced_WhenSafeReasonIsBlank_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new SessionRunStartFenced(" ")).ParamName.ShouldBe("safeReason");

    [Fact]
    public void SessionRunStartFenced_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SessionRunStartFenced("fenced");
        var copy = original with { };
        copy.ShouldBe(original);
        original.SafeReason.ShouldBe("fenced");
    }

    [Fact]
    public void SessionRunStartRejected_WhenSafeReasonIsBlank_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new SessionRunStartRejected(" ")).ParamName.ShouldBe("safeReason");

    [Fact]
    public void SessionRunStartRejected_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SessionRunStartRejected("rejected");
        var copy = original with { };
        copy.ShouldBe(original);
        original.SafeReason.ShouldBe("rejected");
    }
}
