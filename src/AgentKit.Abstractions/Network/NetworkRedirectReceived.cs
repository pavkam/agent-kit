// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Returns a redirect destination that requires fresh resolution and authority.</summary>
public sealed record NetworkRedirectReceived: NetworkSendResult
{
    /// <summary>Initializes an unfollowed redirect.</summary>
    /// <param name="destination">The canonical redirect destination.</param>
    /// <param name="crossOrigin">Whether scheme, host, or port changed.</param>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> is null.</exception>
    public NetworkRedirectReceived(NetworkDestination destination, bool crossOrigin)
    {
        ArgumentNullException.ThrowIfNull(destination);
        Destination = destination;
        CrossOrigin = crossOrigin;
    }

    /// <summary>Gets the destination requiring a new authorization cycle.</summary>
    public NetworkDestination Destination { get; }

    /// <summary>Gets whether origin-bound headers must be removed.</summary>
    public bool CrossOrigin { get; }
}
