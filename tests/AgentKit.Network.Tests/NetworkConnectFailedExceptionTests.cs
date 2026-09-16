// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network.Tests;

/// <summary>Verifies NetworkConnectFailedException behavior and contracts.</summary>
public sealed class NetworkConnectFailedExceptionTests
{
    [Fact]
    public void Constructor_WhenKindOmitted_DefaultsToConnectionFailed()
    {
        var exception = new NetworkConnectFailedException("failed");
        exception.Kind.ShouldBe(NetworkFailureKind.ConnectionFailed);
        exception.Message.ShouldBe("failed");
        exception.InnerException.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WhenKindAndInnerExceptionProvided_RetainsThem()
    {
        var inner = new InvalidOperationException("socket failure");
        var exception = new NetworkConnectFailedException("timed out", NetworkFailureKind.Timeout, inner);
        exception.Kind.ShouldBe(NetworkFailureKind.Timeout);
        exception.InnerException.ShouldBe(inner);
    }

    [Fact]
    public void Constructor_WhenKindIsUndefined_ThrowsWithExactParameterName()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new NetworkConnectFailedException("failed", (NetworkFailureKind) (-1)));
        exception.ParamName.ShouldBe("kind");
    }
}
