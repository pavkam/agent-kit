// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Security;



/// <summary>Verifies SecurityEnforcementIntent behavior and contracts.</summary>
public sealed class SecurityEnforcementIntentTests
{
    [Fact]
    public void SecurityEnforcementIntent_WhenIdentityIsDefault_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new SecurityEnforcementIntent(default, null));
        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe("id");
    }

    [Fact]
    public void SecurityEnforcementIntent_WhenFenceIsDefault_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new SecurityEnforcementIntent(IntentId(), default(FencingToken)));
        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe("requiredFence");
    }

    private static SecurityEnforcementIntentId IntentId() => new(Guid.Parse("40000000-0000-0000-0000-000000000004"));
}
