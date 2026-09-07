// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The response was a redirect; the target destination is reported without being followed.</summary>
/// <remarks>
/// The transport never follows a redirect itself: doing so would connect to
/// a destination the caller never resolved or evaluated against its
/// destination policy. The caller must resolve <see cref="Destination"/>
/// and issue a new request to follow the redirect.
/// </remarks>
public sealed record NetworkRedirectReceived: NetworkSendResult
{
    /// <summary>Initializes a new instance of the <see cref="NetworkRedirectReceived"/> record.</summary>
    /// <param name="destination">The re-canonicalized redirect target destination.</param>
    /// <param name="crossOrigin">
    /// <see langword="true"/> when the redirect target's scheme, host, or
    /// port differs from the original request's destination.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> is null.</exception>
    public NetworkRedirectReceived(NetworkDestination destination, bool crossOrigin)
    {
        ArgumentNullException.ThrowIfNull(destination);

        Destination = destination;
        CrossOrigin = crossOrigin;
    }

    /// <summary>Gets the re-canonicalized redirect target destination.</summary>
    public NetworkDestination Destination { get; init; }

    /// <summary>
    /// Gets a value indicating whether the redirect target's scheme, host,
    /// or port differs from the original request's destination.
    /// </summary>
    public bool CrossOrigin { get; init; }
}
