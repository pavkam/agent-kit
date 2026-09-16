// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Network;

/// <summary>Verifies NetworkResponseTooLargeException behavior and contracts.</summary>
public sealed class NetworkResponseTooLargeExceptionTests
{
    [Fact]
    public void Constructor_WhenMaximumBytesIsNotPositive_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new NetworkResponseTooLargeException(0, 1)).ParamName.ShouldBe("maximumBytes");

    [Fact]
    public void Constructor_WhenObservedBytesDoesNotExceedMaximum_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new NetworkResponseTooLargeException(100, 100)).ParamName.ShouldBe("observedBytes");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var exception = new NetworkResponseTooLargeException(100, 200);
        exception.MaximumBytes.ShouldBe(100);
        exception.ObservedBytes.ShouldBe(200);
        exception.Message.ShouldBe("The response exceeded the configured 100-byte boundary.");
    }
}
