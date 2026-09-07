// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network;

/// <summary>
/// Thrown when reading a response body through <see cref="BoundedReadStream"/>
/// would exceed the request's configured maximum response size.
/// </summary>
/// <remarks>
/// This exception is thrown from the response stream itself rather than
/// returned as a terminal <see cref="NetworkSendResult"/>, because a
/// response's true size can only be known while streaming it, after
/// <see cref="INetworkTransport.SendAsync"/> has already returned a
/// <see cref="NetworkResponseReceived"/> handle to the caller. The stream
/// stops at the bound; no truncated content is ever presented as a
/// complete body.
/// </remarks>
public sealed class NetworkResponseTooLargeException: IOException
{
    /// <summary>Initializes a new instance of the <see cref="NetworkResponseTooLargeException"/> class.</summary>
    /// <param name="observedBytes">The number of bytes read before the bound was reached.</param>
    /// <param name="maximumBytes">The configured maximum response size.</param>
    public NetworkResponseTooLargeException(long observedBytes, long maximumBytes)
        : base($"The response exceeded the maximum allowed size of {maximumBytes} bytes after reading {observedBytes} bytes.")
    {
        ArgumentOutOfRangeException.ThrowIfNegative(observedBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumBytes);

        ObservedBytes = observedBytes;
        MaximumBytes = maximumBytes;
    }

    /// <summary>Gets the number of bytes read before the bound was reached.</summary>
    public long ObservedBytes { get; }

    /// <summary>Gets the configured maximum response size.</summary>
    public long MaximumBytes { get; }
}
