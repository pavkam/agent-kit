// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Tests.Egress;

using AgentKit.Providers.Egress;

/// <summary>Verifies <see cref="ProviderEgressBodyFault"/> classification of response-body faults.</summary>
public sealed class ProviderEgressBodyFaultTests
{
    [Fact]
    public void Classify_WhenBodyDeadlineExpired_ReturnsTimeoutWithBoundedMessage()
    {
        var kind = ProviderEgressBodyFault.Classify(new NetworkResponseTimedOutException(), out var message);

        kind.ShouldBe(ProviderFailureKind.Timeout);
        message.ShouldBe("The response body was not fully received before its deadline.");
    }

    [Fact]
    public void Classify_WhenStreamedBodyOverranItsBound_ReturnsProtocolViolationNotTruncatedSuccess()
    {
        var kind = ProviderEgressBodyFault.Classify(new NetworkResponseTooLargeException(4, 5), out var message);

        kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        message.ShouldBe("The response body exceeded the configured response bound.");
    }

    [Fact]
    public void Classify_WhenAnyOtherIoFault_ReturnsUnavailable()
    {
        var kind = ProviderEgressBodyFault.Classify(new IOException("Connection reset by peer."), out var message);

        kind.ShouldBe(ProviderFailureKind.Unavailable);
        message.ShouldNotContain("Connection reset");
    }

    [Fact]
    public void Classify_WhenExceptionIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => ProviderEgressBodyFault.Classify(null!, out _));

        exception.ParamName.ShouldBe("exception");
    }
}
