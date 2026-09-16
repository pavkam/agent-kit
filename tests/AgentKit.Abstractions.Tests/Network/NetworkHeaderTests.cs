// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Network;

/// <summary>Verifies NetworkHeader behavior and contracts.</summary>
public sealed class NetworkHeaderTests
{
    [Fact]
    public void Constructor_WhenNameIsBlank_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new NetworkHeader(" ", "value")).ParamName.ShouldBe("name");

    [Fact]
    public void Constructor_WhenValueIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new NetworkHeader("name", null!)).ParamName.ShouldBe("value");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var header = new NetworkHeader("X-Test", "value");
        header.Name.ShouldBe("X-Test");
        header.Value.ShouldBe("value");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new NetworkHeader("X-Test", "value");
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
