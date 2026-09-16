// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

/// <summary>Verifies SessionProfileReference behavior and contracts.</summary>
public sealed class SessionProfileReferenceTests
{
    [Fact]
    public void Constructor_WhenKeyIsBlank_ThrowsExactArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new SessionProfileReference(default, new SessionProfileVersion(1)));
        exception.ParamName.ShouldBe("key");
    }

    [Fact]
    public void Constructor_WhenVersionIsDefault_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new SessionProfileReference(new SessionProfileKey("profile"), default));
        exception.ParamName.ShouldBe("version");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var reference = new SessionProfileReference(new SessionProfileKey("profile"), new SessionProfileVersion(2));
        reference.Key.ShouldBe(new SessionProfileKey("profile"));
        reference.Version.ShouldBe(new SessionProfileVersion(2));
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SessionProfileReference(new SessionProfileKey("profile"), new SessionProfileVersion(2));
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
