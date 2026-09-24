// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client;

/// <summary>Immutable MCP client mechanics captured for the provider lifetime.</summary>
internal sealed record McpClientOptionsSnapshot
{
    internal McpClientOptionsSnapshot(
        TimeSpan handshakeTimeout,
        TimeSpan requestTimeout,
        TimeSpan shutdownTimeout,
        int maximumFrameBytes,
        int maximumMessageBytes,
        int maximumInFlightRequests,
        McpUnknownNotificationPolicy unknownNotificationPolicy)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(handshakeTimeout, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(requestTimeout, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(shutdownTimeout, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumFrameBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumMessageBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumInFlightRequests);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maximumFrameBytes, maximumMessageBytes);

        HandshakeTimeout = handshakeTimeout;
        RequestTimeout = requestTimeout;
        ShutdownTimeout = shutdownTimeout;
        MaximumFrameBytes = maximumFrameBytes;
        MaximumMessageBytes = maximumMessageBytes;
        MaximumInFlightRequests = maximumInFlightRequests;
        UnknownNotificationPolicy = unknownNotificationPolicy;
    }

    internal TimeSpan HandshakeTimeout { get; }

    internal TimeSpan RequestTimeout { get; }

    internal TimeSpan ShutdownTimeout { get; }

    internal int MaximumFrameBytes { get; }

    internal int MaximumMessageBytes { get; }

    internal int MaximumInFlightRequests { get; }

    internal McpUnknownNotificationPolicy UnknownNotificationPolicy { get; }

    internal McpEndpointBounds CreateEndpointBounds() => new(
        HandshakeTimeout,
        RequestTimeout,
        ShutdownTimeout,
        MaximumFrameBytes,
        MaximumMessageBytes,
        MaximumInFlightRequests);
}
