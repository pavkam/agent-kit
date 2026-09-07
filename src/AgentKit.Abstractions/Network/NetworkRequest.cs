// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>One complete, immutable request to send against a network destination.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization.
/// </remarks>
public sealed record NetworkRequest
{
    /// <summary>Initializes a new instance of the <see cref="NetworkRequest"/> record.</summary>
    /// <param name="id">The identity of this request operation.</param>
    /// <param name="method">The request method.</param>
    /// <param name="destination">The destination to send to.</param>
    /// <param name="headers">The request headers.</param>
    /// <param name="content">The request body, when applicable.</param>
    /// <param name="bounds">The bounds this request must respect.</param>
    /// <param name="resolvedAddresses">The exact non-empty address set returned by protected resolution.</param>
    /// <param name="classification">The maximum sensitivity of intentionally transmitted data.</param>
    /// <param name="grant">The exact single-use egress authority.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="destination"/>, <paramref name="headers"/>, or <paramref name="bounds"/> is null.
    /// </exception>
    public NetworkRequest(
        NetworkOperationId id,
        NetworkMethod method,
        NetworkDestination destination,
        NetworkHeaderSet headers,
        NetworkRequestContent? content,
        NetworkBounds bounds,
        ImmutableArray<NetworkAddress> resolvedAddresses,
        NetworkDataClassification classification,
        SecurityGrant grant)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(bounds);
        ArgumentException.ThrowIfDefaultOrEmpty(resolvedAddresses);
        ArgumentOutOfRangeException.ThrowIfUndefined(classification);
        ArgumentNullException.ThrowIfNull(grant);

        Id = id;
        Method = method;
        Destination = destination;
        Headers = headers;
        Content = content;
        Bounds = bounds;
        ResolvedAddresses = resolvedAddresses;
        Classification = classification;
        Grant = grant;
    }

    /// <summary>Gets the identity of this request operation.</summary>
    public NetworkOperationId Id { get; }

    /// <summary>Gets the request method.</summary>
    public NetworkMethod Method { get; }

    /// <summary>Gets the destination to send to.</summary>
    public NetworkDestination Destination { get; }

    /// <summary>Gets the request headers.</summary>
    public NetworkHeaderSet Headers { get; }

    /// <summary>Gets the request body, when applicable.</summary>
    public NetworkRequestContent? Content { get; }

    /// <summary>Gets the bounds this request must respect.</summary>
    public NetworkBounds Bounds { get; }

    /// <summary>Gets the exact resolved addresses eligible for connection.</summary>
    public ImmutableArray<NetworkAddress> ResolvedAddresses { get; }
    /// <summary>Gets the maximum sensitivity of intentionally transmitted data.</summary>
    public NetworkDataClassification Classification { get; }
    /// <summary>Gets the exact single-use egress authority.</summary>
    public SecurityGrant Grant { get; }
}
