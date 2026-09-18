// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Json.Tests;

/// <summary>Verifies <see cref="DirectoryAccessDenied"/> construction, guards, and equality.</summary>
public sealed class DirectoryAccessDeniedTests
{
    /// <summary>Verifies a blank safe message is rejected.</summary>
    [Fact]
    public void Constructor_WhenSafeMessageIsBlank_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new DirectoryAccessDenied(" "));
        exception.ParamName.ShouldBe("safeMessage");
    }

    /// <summary>Verifies the constructed value retains its safe message and clones equivalently.</summary>
    [Fact]
    public void DirectoryAccessDenied_WhenConstructed_RetainsSafeMessageAndClonesEquivalently()
    {
        var denied = new DirectoryAccessDenied("denied reason");

        denied.SafeMessage.ShouldBe("denied reason");
        var clone = denied with { };
        clone.ShouldBe(denied);
    }

    /// <summary>Verifies a different safe message breaks equality.</summary>
    [Fact]
    public void Equals_WhenSafeMessageDiffers_IsNotEqual()
    {
        var left = new DirectoryAccessDenied("one reason");
        var right = new DirectoryAccessDenied("another reason");

        left.ShouldNotBe(right);
    }
}
