// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Network;

/// <summary>Verifies NetworkHeaderSet behavior and contracts.</summary>
public sealed class NetworkHeaderSetTests
{
    [Fact]
    public void Constructor_WhenHeadersIsDefault_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new NetworkHeaderSet(default)).ParamName.ShouldBe("headers");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsHeaders()
    {
        var header = new NetworkHeader("Content-Type", "text/plain");
        var set = new NetworkHeaderSet([header]);
        set.Headers.ShouldBe([header]);
    }

    [Fact]
    public void GetValues_WhenNameIsBlank_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => NetworkHeaderSet.Empty.GetValues(" ")).ParamName.ShouldBe("name");

    [Fact]
    public void GetValues_WhenNameMatchesCaseInsensitively_ReturnsMatchingValuesInOrder()
    {
        var set = new NetworkHeaderSet([
            new NetworkHeader("X-Test", "one"),
            new NetworkHeader("Other", "ignored"),
            new NetworkHeader("x-test", "two"),
        ]);

        set.GetValues("X-TEST").ShouldBe(["one", "two"]);
    }

    [Fact]
    public void Equality_WhenEquivalentArraysDifferByInstance_IsStructurallyEqual()
    {
        var left = new NetworkHeaderSet([new NetworkHeader("X-Test", "one")]);
        var right = new NetworkHeaderSet([new NetworkHeader("X-Test", "one")]);
        left.ShouldBe(right);
        left.GetHashCode().ShouldBe(right.GetHashCode());
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new NetworkHeaderSet([new NetworkHeader("X-Test", "one")]);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
