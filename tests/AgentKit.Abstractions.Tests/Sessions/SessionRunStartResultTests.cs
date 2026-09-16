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
    public void SessionRunStateLoaded_WhenStateIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new SessionRunStateLoaded(null!)).ParamName.ShouldBe("state");

    [Fact]
    public void SessionRunAccepted_WhenStateIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new SessionRunAccepted(null!, new SessionVersion(1), existing: false)).ParamName.ShouldBe("state");
}
