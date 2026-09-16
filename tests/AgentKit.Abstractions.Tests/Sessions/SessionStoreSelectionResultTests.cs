// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

/// <summary>Verifies SessionStoreSelectionResult derived behavior and contracts.</summary>
public sealed class SessionStoreSelectionResultTests
{
    [Fact]
    public void SessionStoreSelectionRejected_WhenReasonIsUndefined_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new SessionStoreSelectionRejected((SessionStoreSelectionRejectionReason) 99, "reason"));
        exception.ParamName.ShouldBe("reason");
    }

    [Fact]
    public void SessionStoreSelectionRejected_WhenSafeMessageIsBlank_ThrowsExactArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new SessionStoreSelectionRejected(SessionStoreSelectionRejectionReason.MissingStoreKey, " "));
        exception.ParamName.ShouldBe("safeMessage");
    }

    [Fact]
    public void SessionStoreSelectionRejected_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var rejected = new SessionStoreSelectionRejected(SessionStoreSelectionRejectionReason.IncompatibleDurability, "rejected");
        rejected.Reason.ShouldBe(SessionStoreSelectionRejectionReason.IncompatibleDurability);
        rejected.SafeMessage.ShouldBe("rejected");
    }

    [Fact]
    public void SessionStoreSelectionRejected_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SessionStoreSelectionRejected(SessionStoreSelectionRejectionReason.IncompatibleCapabilities, "rejected");
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
