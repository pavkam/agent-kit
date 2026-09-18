// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Json.Tests;

/// <summary>Verifies <see cref="DirectoryAccessAllowed"/> construction and equality against sibling outcomes.</summary>
public sealed class DirectoryAccessAllowedTests
{
    /// <summary>Verifies a structural clone equals the original.</summary>
    [Fact]
    public void DirectoryAccessAllowed_WhenCloned_ProducesAnEquivalentInstance()
    {
        var allowed = new DirectoryAccessAllowed();

        var clone = allowed with { };

        clone.ShouldBe(allowed);
        _ = clone.ShouldBeOfType<DirectoryAccessAllowed>();
    }

    /// <summary>Verifies an allowed outcome never compares equal to a different derived outcome or null.</summary>
    [Fact]
    public void DirectoryAccessAllowed_WhenComparedAsBaseType_IsNotEqualToADifferentDerivedType()
    {
        DirectoryAccessResult allowed = new DirectoryAccessAllowed();
        DirectoryAccessResult denied = new DirectoryAccessDenied("denied");
        DirectoryAccessResult unavailable = new DirectoryAccessUnavailable("unavailable");

        allowed.Equals(denied).ShouldBeFalse();
        allowed.Equals(unavailable).ShouldBeFalse();
        allowed.Equals(null).ShouldBeFalse();
        allowed.Equals(new DirectoryAccessAllowed()).ShouldBeTrue();
    }
}
