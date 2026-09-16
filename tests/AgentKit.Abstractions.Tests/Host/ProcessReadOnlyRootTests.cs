// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;

/// <summary>Verifies ProcessReadOnlyRoot behavior and contracts.</summary>
public sealed class ProcessReadOnlyRootTests
{
    [Fact]
    public void Constructor_WhenProfileIdIsBlank_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new ProcessReadOnlyRoot(" ", "/opt")).ParamName.ShouldBe("profileId");

    [Fact]
    public void Constructor_WhenAbsolutePathIsRelative_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new ProcessReadOnlyRoot("toolchain", "relative")).ParamName.ShouldBe("absolutePath");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var root = new ProcessReadOnlyRoot("toolchain", "/opt/toolchain");
        root.ProfileId.ShouldBe("toolchain");
        root.AbsolutePath.ShouldBe("/opt/toolchain");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ProcessReadOnlyRoot("toolchain", "/opt/toolchain");
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
