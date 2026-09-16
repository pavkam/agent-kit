// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Network;

/// <summary>Verifies NetworkRedirectLimitExceeded behavior and contracts.</summary>
public sealed class NetworkRedirectLimitExceededTests
{
    [Fact]
    public void Constructor_WhenMaximumRedirectsIsNegative_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new NetworkRedirectLimitExceeded(-1)).ParamName.ShouldBe("maximumRedirects");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsMaximumRedirects()
    {
        var exceeded = new NetworkRedirectLimitExceeded(5);
        exceeded.MaximumRedirects.ShouldBe(5);
        NetworkSendResult result = exceeded;
        _ = result.ShouldBeOfType<NetworkRedirectLimitExceeded>();
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new NetworkRedirectLimitExceeded(5);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
