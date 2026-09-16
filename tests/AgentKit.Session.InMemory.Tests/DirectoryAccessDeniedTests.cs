// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.InMemory.Tests;

/// <summary>Verifies DirectoryAccessDenied behavior and contracts.</summary>
public sealed class DirectoryAccessDeniedTests
{
    [Fact]
    public void Constructor_WhenSafeMessageIsBlank_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new DirectoryAccessDenied(" "));
        exception.ParamName.ShouldBe("safeMessage");
    }

    [Fact]
    public void DirectoryAccessDenied_WhenConstructed_RetainsSafeMessageAndClonesEquivalently()
    {
        var denied = new DirectoryAccessDenied("denied reason");

        denied.SafeMessage.ShouldBe("denied reason");
        var clone = denied with { };
        clone.ShouldBe(denied);
    }
}
