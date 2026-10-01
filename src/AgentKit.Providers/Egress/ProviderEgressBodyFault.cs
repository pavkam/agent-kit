// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Egress;

/// <summary>Maps a failure raised while reading a provider response body to the stable provider failure taxonomy.</summary>
/// <remarks>
/// The network transport reports a streamed overrun as <see cref="NetworkResponseTooLargeException"/> and an expired
/// body deadline as <see cref="NetworkResponseTimedOutException"/>; neither may surface as truncated success or as a
/// generic connection fault. Every other <see cref="IOException"/> is a connection fault.
/// </remarks>
public static class ProviderEgressBodyFault
{
    /// <summary>Classifies one body read fault.</summary>
    /// <param name="exception">The fault thrown by the response body stream.</param>
    /// <param name="safeMessage">Receives a bounded message that contains no response content.</param>
    /// <returns>
    /// <see cref="ProviderFailureKind.Timeout"/> for an expired body deadline,
    /// <see cref="ProviderFailureKind.ProtocolViolation"/> for a streamed overrun, otherwise
    /// <see cref="ProviderFailureKind.Unavailable"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="exception"/> is null.</exception>
    public static ProviderFailureKind Classify(IOException exception, out string safeMessage)
    {
        ArgumentNullException.ThrowIfNull(exception);
        switch (exception)
        {
            case NetworkResponseTimedOutException:
                safeMessage = "The response body was not fully received before its deadline.";
                return ProviderFailureKind.Timeout;
            case NetworkResponseTooLargeException:
                safeMessage = "The response body exceeded the configured response bound.";
                return ProviderFailureKind.ProtocolViolation;
            default:
                safeMessage = "The connection failed while the response body was being received.";
                return ProviderFailureKind.Unavailable;
        }
    }
}
