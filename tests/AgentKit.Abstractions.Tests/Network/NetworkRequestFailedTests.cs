// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Network;

/// <summary>Verifies NetworkRequestFailed behavior and contracts.</summary>
public sealed class NetworkRequestFailedTests
{
    [Fact]
    public void Constructor_WhenKindIsUndefined_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new NetworkRequestFailed((NetworkFailureKind) 99, "safe", false)).ParamName.ShouldBe("kind");

    [Fact]
    public void Constructor_WhenSafeMessageIsBlank_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new NetworkRequestFailed(NetworkFailureKind.ConnectionFailed, " ", false)).ParamName.ShouldBe("safeMessage");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var failed = new NetworkRequestFailed(NetworkFailureKind.ConnectionFailed, "safe", true);
        failed.Kind.ShouldBe(NetworkFailureKind.ConnectionFailed);
        failed.SafeMessage.ShouldBe("safe");
        failed.SideEffectCertain.ShouldBeTrue();
        NetworkSendResult result = failed;
        _ = result.ShouldBeOfType<NetworkRequestFailed>();
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new NetworkRequestFailed(NetworkFailureKind.ConnectionFailed, "safe", true);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
