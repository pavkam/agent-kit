// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>Captures the immutable bounds applied to one MCP endpoint.</summary>
/// <remarks>
/// These bounds are copied from client options when the endpoint is registered
/// and are not re-read from mutable options while a session is open. They bound
/// handshake, request, and shutdown waits, frame and message size, and how many
/// correlated requests may be in flight. They do not authorize a transport.
/// </remarks>
public sealed record McpEndpointBounds
{
    /// <summary>Initializes positive endpoint bounds.</summary>
    /// <param name="handshakeTimeout">The positive limit for protocol initialization.</param>
    /// <param name="requestTimeout">The positive limit for one correlated request.</param>
    /// <param name="shutdownTimeout">The positive limit for session disposal.</param>
    /// <param name="maximumFrameBytes">The positive maximum size of one transport frame.</param>
    /// <param name="maximumMessageBytes">The positive maximum size of one protocol message.</param>
    /// <param name="maximumInFlightRequests">The positive maximum number of concurrent correlated requests.</param>
    /// <exception cref="ArgumentOutOfRangeException">A timeout is not positive, or a size or concurrency limit is not positive.</exception>
    public McpEndpointBounds(
        TimeSpan handshakeTimeout,
        TimeSpan requestTimeout,
        TimeSpan shutdownTimeout,
        int maximumFrameBytes,
        int maximumMessageBytes,
        int maximumInFlightRequests)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(handshakeTimeout, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(requestTimeout, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(shutdownTimeout, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumFrameBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumMessageBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumInFlightRequests);

        HandshakeTimeout = handshakeTimeout;
        RequestTimeout = requestTimeout;
        ShutdownTimeout = shutdownTimeout;
        MaximumFrameBytes = maximumFrameBytes;
        MaximumMessageBytes = maximumMessageBytes;
        MaximumInFlightRequests = maximumInFlightRequests;
    }

    /// <summary>Gets the positive protocol-initialization limit.</summary>
    public TimeSpan HandshakeTimeout { get; }

    /// <summary>Gets the positive per-request limit.</summary>
    public TimeSpan RequestTimeout { get; }

    /// <summary>Gets the positive session-disposal limit.</summary>
    public TimeSpan ShutdownTimeout { get; }

    /// <summary>Gets the positive maximum transport-frame size, in bytes.</summary>
    public int MaximumFrameBytes { get; }

    /// <summary>Gets the positive maximum protocol-message size, in bytes.</summary>
    public int MaximumMessageBytes { get; }

    /// <summary>Gets the positive maximum number of concurrent correlated requests.</summary>
    public int MaximumInFlightRequests { get; }
}
