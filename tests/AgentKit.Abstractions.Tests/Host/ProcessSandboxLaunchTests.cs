// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;

/// <summary>Verifies ProcessSandboxLaunch behavior and contracts.</summary>
public sealed class ProcessSandboxLaunchTests
{
    [Fact]
    public void Constructor_WhenExecutablePathIsBlank_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new ProcessSandboxLaunch(" ", ["a"])).ParamName.ShouldBe("executablePath");

    [Fact]
    public void Constructor_WhenExecutablePathIsRelative_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new ProcessSandboxLaunch("relative/path", ["a"])).ParamName.ShouldBe("executablePath");

    [Fact]
    public void Constructor_WhenArgumentsAreDefault_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new ProcessSandboxLaunch("/usr/bin/sandbox", default)).ParamName.ShouldBe("arguments");

    [Fact]
    public void Constructor_WhenArgumentsContainNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new ProcessSandboxLaunch("/usr/bin/sandbox", ["a", null!])).ParamName.ShouldBe("arguments");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var launch = new ProcessSandboxLaunch("/usr/bin/sandbox", ["--profile", "a"]);
        launch.ExecutablePath.ShouldBe("/usr/bin/sandbox");
        launch.Arguments.ShouldBe(["--profile", "a"]);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ProcessSandboxLaunch("/usr/bin/sandbox", ["--profile"]);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
