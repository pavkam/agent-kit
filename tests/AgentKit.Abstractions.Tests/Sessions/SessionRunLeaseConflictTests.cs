// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;
/// <summary>Verifies SessionRunLeaseConflict behavior and contracts.</summary>
public sealed class SessionRunLeaseConflictTests
{
    [Fact]
    public void LeaseOutcomeConstructors_WhenArgumentsAreInvalid_ThrowExactExceptions()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new SessionRunLeaseConflict((SessionRunLeaseConflictKind) 42, "reason")).ParamName.ShouldBe("kind");
        Should.Throw<ArgumentException>(() => new SessionRunLeaseConflict(SessionRunLeaseConflictKind.AcceptedState, " ")).ParamName.ShouldBe("safeReason");
    }
}
