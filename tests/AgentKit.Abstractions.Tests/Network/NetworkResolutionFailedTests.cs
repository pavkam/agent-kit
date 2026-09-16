// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Network;

/// <summary>Verifies NetworkResolutionFailed behavior and contracts.</summary>
public sealed class NetworkResolutionFailedTests
{
    [Fact]
    public void Constructor_WhenKindIsUndefined_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new NetworkResolutionFailed((NetworkFailureKind) 99, "safe")).ParamName.ShouldBe("kind");

    [Fact]
    public void Constructor_WhenSafeMessageIsBlank_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new NetworkResolutionFailed(NetworkFailureKind.DnsResolutionFailed, " ")).ParamName.ShouldBe("safeMessage");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var failed = new NetworkResolutionFailed(NetworkFailureKind.DnsResolutionFailed, "safe");
        failed.Kind.ShouldBe(NetworkFailureKind.DnsResolutionFailed);
        failed.SafeMessage.ShouldBe("safe");
        NetworkResolutionResult result = failed;
        _ = result.ShouldBeOfType<NetworkResolutionFailed>();
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new NetworkResolutionFailed(NetworkFailureKind.DnsResolutionFailed, "safe");
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
