// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

using AgentKit;

/// <summary>Verifies IdentityFailure behavior and contracts.</summary>
public sealed class IdentityFailureTests
{
    [Fact]
    public void IdentityFailure_WhenMessageIsBlank_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new IdentityFailure(IdentityFailureKind.Malformed, " "));
        exception.ParamName.ShouldBe("safeMessage");
    }
}
