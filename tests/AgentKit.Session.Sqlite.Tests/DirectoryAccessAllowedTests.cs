// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Sqlite.Tests;

/// <summary>Verifies DirectoryAccessAllowed behavior and contracts.</summary>
public sealed class DirectoryAccessAllowedTests
{
    [Fact]
    public void DirectoryAccessAllowed_WhenCloned_ProducesAnEquivalentInstance()
    {
        var allowed = new DirectoryAccessAllowed();

        var clone = allowed with { };

        clone.ShouldBe(allowed);
        _ = clone.ShouldBeOfType<DirectoryAccessAllowed>();
    }

    [Fact]
    public void DirectoryAccessAllowed_WhenComparedAsBaseType_IsNotEqualToADifferentDerivedType()
    {
        DirectoryAccessResult allowed = new DirectoryAccessAllowed();
        DirectoryAccessResult denied = new DirectoryAccessDenied("denied");

        allowed.Equals(denied).ShouldBeFalse();
        allowed.Equals(null).ShouldBeFalse();
        allowed.Equals(new DirectoryAccessAllowed()).ShouldBeTrue();
    }
}
