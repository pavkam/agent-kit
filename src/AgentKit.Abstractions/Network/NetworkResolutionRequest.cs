// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>One complete, immutable request to resolve a destination's addresses.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization.
/// </remarks>
public sealed record NetworkResolutionRequest
{
    /// <summary>Initializes a new instance of the <see cref="NetworkResolutionRequest"/> record.</summary>
    /// <param name="id">The identity of this resolution operation.</param>
    /// <param name="destination">The destination to resolve.</param>
    /// <param name="bounds">The bounds this resolution must respect.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="destination"/> or <paramref name="bounds"/> is null.
    /// </exception>
    public NetworkResolutionRequest(NetworkOperationId id, NetworkDestination destination, NetworkBounds bounds)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(bounds);

        Id = id;
        Destination = destination;
        Bounds = bounds;
    }

    /// <summary>Gets the identity of this resolution operation.</summary>
    public NetworkOperationId Id { get; init; }

    /// <summary>Gets the destination to resolve.</summary>
    public NetworkDestination Destination { get; init; }

    /// <summary>Gets the bounds this resolution must respect.</summary>
    public NetworkBounds Bounds { get; init; }
}
