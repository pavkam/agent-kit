// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

/// <summary>Verifies SessionRunLeaseConflict behavior and contracts.</summary>
public sealed class SessionRunLeaseConflictTests
{
    [Fact]
    public void Constructor_WhenKindIsUndefined_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new SessionRunLeaseConflict((SessionRunLeaseConflictKind) 99, "reason"));
        exception.ParamName.ShouldBe("kind");
    }

    [Fact]
    public void Constructor_WhenSafeReasonIsBlank_ThrowsExactArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new SessionRunLeaseConflict(SessionRunLeaseConflictKind.SessionProfile, " "));
        exception.ParamName.ShouldBe("safeReason");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var conflict = new SessionRunLeaseConflict(SessionRunLeaseConflictKind.AcceptedState, "conflict");
        conflict.Kind.ShouldBe(SessionRunLeaseConflictKind.AcceptedState);
        conflict.SafeReason.ShouldBe("conflict");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SessionRunLeaseConflict(SessionRunLeaseConflictKind.OperationStateRevision, "conflict");
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
