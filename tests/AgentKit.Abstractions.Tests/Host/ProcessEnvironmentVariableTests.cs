// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;

/// <summary>Verifies ProcessEnvironmentVariable behavior and contracts.</summary>
public sealed class ProcessEnvironmentVariableTests
{
    [Fact]
    public void Constructor_WhenNameIsBlank_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new ProcessEnvironmentVariable(" ", "value")).ParamName.ShouldBe("name");

    [Fact]
    public void Constructor_WhenValueIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new ProcessEnvironmentVariable("NAME", null!)).ParamName.ShouldBe("value");

    [Fact]
    public void Constructor_WhenNameContainsNul_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new ProcessEnvironmentVariable("NAME\0", "value")).ParamName.ShouldBe("name");

    [Fact]
    public void Constructor_WhenValueContainsNul_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new ProcessEnvironmentVariable("NAME", "value\0")).ParamName.ShouldBe("value");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var variable = new ProcessEnvironmentVariable("NAME", "value");
        variable.Name.ShouldBe("NAME");
        variable.Value.ShouldBe("value");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ProcessEnvironmentVariable("NAME", "value");
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
