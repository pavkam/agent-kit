// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Security;

/// <summary>Verifies RevocationReason behavior and contracts.</summary>
public sealed class RevocationReasonTests
{
    [Fact]
    public void Constructor_WhenTriggerIsUndefined_ThrowsExactArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new RevocationReason((SecurityRevocationTrigger) 99, "message")).ParamName.ShouldBe("trigger");

    [Fact]
    public void Constructor_WhenSafeMessageIsBlank_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new RevocationReason(SecurityRevocationTrigger.Explicit, " ")).ParamName.ShouldBe("safeMessage");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var reason = new RevocationReason(SecurityRevocationTrigger.PrincipalDisabled, "message");
        reason.Trigger.ShouldBe(SecurityRevocationTrigger.PrincipalDisabled);
        reason.SafeMessage.ShouldBe("message");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new RevocationReason(SecurityRevocationTrigger.Explicit, "message");
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
