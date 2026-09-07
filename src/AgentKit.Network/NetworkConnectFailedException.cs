// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network;

/// <summary>
/// Thrown by <see cref="DefaultNetworkTransport"/>'s connect callback when a
/// connection cannot be established to a resolved address.
/// </summary>
/// <remarks>
/// A failure at this stage can only mean no request bytes were ever sent,
/// so <see cref="DefaultNetworkTransport"/> maps this exception to a
/// <see cref="NetworkRequestFailed"/> with
/// <see cref="NetworkRequestFailed.SideEffectCertain"/> set to
/// <see langword="true"/>.
/// </remarks>
internal sealed class NetworkConnectFailedException: Exception
{
    /// <summary>Initializes a new instance of the <see cref="NetworkConnectFailedException"/> class.</summary>
    /// <param name="message">A description of the connection failure.</param>
    public NetworkConnectFailedException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="NetworkConnectFailedException"/> class.</summary>
    /// <param name="message">A description of the connection failure.</param>
    /// <param name="innerException">The underlying exception that caused the connection failure.</param>
    public NetworkConnectFailedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
