// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The request completed and the caller owns the returned response handle.</summary>
public sealed record NetworkResponseReceived: NetworkSendResult
{
    /// <summary>Initializes a new instance of the <see cref="NetworkResponseReceived"/> record.</summary>
    /// <param name="response">The owned response handle.</param>
    /// <exception cref="ArgumentNullException"><paramref name="response"/> is null.</exception>
    public NetworkResponseReceived(INetworkResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        Response = response;
    }

    /// <summary>Gets the owned response handle.</summary>
    public INetworkResponse Response { get; init; }
}
