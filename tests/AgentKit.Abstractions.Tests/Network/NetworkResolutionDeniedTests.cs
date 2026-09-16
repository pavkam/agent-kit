// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Network;

/// <summary>Verifies NetworkResolutionDenied behavior and contracts.</summary>
public sealed class NetworkResolutionDeniedTests
{
    [Fact]
    public void Constructor_WhenSafeMessageIsBlank_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new NetworkResolutionDenied(" ")).ParamName.ShouldBe("safeMessage");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsSafeMessage()
    {
        var denied = new NetworkResolutionDenied("denied");
        denied.SafeMessage.ShouldBe("denied");
        NetworkResolutionResult result = denied;
        _ = result.ShouldBeOfType<NetworkResolutionDenied>();
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new NetworkResolutionDenied("denied");
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
