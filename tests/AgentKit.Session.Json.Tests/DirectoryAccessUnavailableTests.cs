// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Json.Tests;

/// <summary>Verifies <see cref="DirectoryAccessUnavailable"/> construction, guards, and equality.</summary>
public sealed class DirectoryAccessUnavailableTests
{
    /// <summary>Verifies a blank safe message is rejected.</summary>
    [Fact]
    public void Constructor_WhenSafeMessageIsBlank_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new DirectoryAccessUnavailable(" "));
        exception.ParamName.ShouldBe("safeMessage");
    }

    /// <summary>Verifies the constructed value retains its safe message and clones equivalently.</summary>
    [Fact]
    public void DirectoryAccessUnavailable_WhenConstructed_RetainsSafeMessageAndClonesEquivalently()
    {
        var unavailable = new DirectoryAccessUnavailable("unavailable reason");

        unavailable.SafeMessage.ShouldBe("unavailable reason");
        var clone = unavailable with { };
        clone.ShouldBe(unavailable);
    }

    /// <summary>Verifies a different safe message breaks equality.</summary>
    [Fact]
    public void Equals_WhenSafeMessageDiffers_IsNotEqual()
    {
        var left = new DirectoryAccessUnavailable("one reason");
        var right = new DirectoryAccessUnavailable("another reason");

        left.ShouldNotBe(right);
    }
}
