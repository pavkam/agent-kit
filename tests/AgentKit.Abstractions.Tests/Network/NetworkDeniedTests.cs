// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Network;

/// <summary>Verifies NetworkDenied behavior and contracts.</summary>
public sealed class NetworkDeniedTests
{
    [Fact]
    public void Constructor_WhenSafeMessageIsBlank_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new NetworkDenied(" ")).ParamName.ShouldBe("safeMessage");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsSafeMessage()
    {
        var denied = new NetworkDenied("denied");
        denied.SafeMessage.ShouldBe("denied");
        NetworkSendResult result = denied;
        _ = result.ShouldBeOfType<NetworkDenied>();
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new NetworkDenied("denied");
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
