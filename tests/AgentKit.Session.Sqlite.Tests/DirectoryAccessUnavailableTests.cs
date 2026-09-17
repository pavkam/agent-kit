// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Sqlite.Tests;

/// <summary>Verifies DirectoryAccessUnavailable behavior and contracts.</summary>
public sealed class DirectoryAccessUnavailableTests
{
    [Fact]
    public void Constructor_WhenSafeMessageIsBlank_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new DirectoryAccessUnavailable(" "));
        exception.ParamName.ShouldBe("safeMessage");
    }

    [Fact]
    public void DirectoryAccessUnavailable_WhenConstructed_RetainsSafeMessageAndClonesEquivalently()
    {
        var unavailable = new DirectoryAccessUnavailable("unavailable reason");

        unavailable.SafeMessage.ShouldBe("unavailable reason");
        var clone = unavailable with { };
        clone.ShouldBe(unavailable);
    }
}
