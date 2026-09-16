// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Network;

/// <summary>Verifies NetworkResponseTimedOutException behavior and contracts.</summary>
public sealed class NetworkResponseTimedOutExceptionTests
{
    [Fact]
    public void Constructor_WhenParameterless_HasStableMessage()
    {
        var exception = new NetworkResponseTimedOutException();
        exception.Message.ShouldBe("The response body exceeded its configured deadline.");
        exception.InnerException.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WhenInnerExceptionIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new NetworkResponseTimedOutException(null!)).ParamName.ShouldBe("innerException");

    [Fact]
    public void Constructor_WhenInnerExceptionIsProvided_RetainsInnerException()
    {
        var inner = new OperationCanceledException();
        var exception = new NetworkResponseTimedOutException(inner);
        exception.InnerException.ShouldBeSameAs(inner);
        exception.Message.ShouldBe("The response body exceeded its configured deadline.");
    }
}
