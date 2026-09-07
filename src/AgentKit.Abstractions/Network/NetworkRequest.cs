// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>One complete, immutable request to send against a network destination.</summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization.
/// </para>
/// <para>
/// <see cref="INetworkTransport"/> never performs DNS resolution itself, so
/// a request carries the non-empty, already-resolved, still-valid
/// candidate addresses a prior <see cref="INetworkNameResolver"/>
/// resolution produced. The transport selects the first candidate that has
/// not expired and connects only to that address; it never re-resolves the
/// destination host at connect time.
/// </para>
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
    /// <param name="resolvedAddresses">The non-empty, already-resolved candidate addresses.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="destination"/>, <paramref name="headers"/>, or <paramref name="bounds"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="resolvedAddresses"/> is a default, uninitialized array, or is empty.
    /// </exception>
    public NetworkRequest(
        NetworkOperationId id,
        NetworkMethod method,
        NetworkDestination destination,
        NetworkHeaderSet headers,
        NetworkRequestContent? content,
        NetworkBounds bounds,
        ImmutableArray<NetworkAddress> resolvedAddresses)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(bounds);
        ArgumentException.ThrowIfDefault(resolvedAddresses);
        if (resolvedAddresses.IsEmpty)
        {
            throw new ArgumentException("At least one resolved address is required.", nameof(resolvedAddresses));
        }

        Id = id;
        Method = method;
        Destination = destination;
        Headers = headers;
        Content = content;
        Bounds = bounds;
        ResolvedAddresses = resolvedAddresses;
    }

    /// <summary>Gets the identity of this request operation.</summary>
    public NetworkOperationId Id { get; init; }

    /// <summary>Gets the request method.</summary>
    public NetworkMethod Method { get; init; }

    /// <summary>Gets the destination to send to.</summary>
    public NetworkDestination Destination { get; init; }

    /// <summary>Gets the request headers.</summary>
    public NetworkHeaderSet Headers { get; init; }

    /// <summary>Gets the request body, when applicable.</summary>
    public NetworkRequestContent? Content { get; init; }

    /// <summary>Gets the bounds this request must respect.</summary>
    public NetworkBounds Bounds { get; init; }

    /// <summary>Gets the non-empty, already-resolved candidate addresses.</summary>
    public ImmutableArray<NetworkAddress> ResolvedAddresses { get; init; }

    /// <inheritdoc/>
    public bool Equals(NetworkRequest? other) =>
        other is not null
        && Id.Equals(other.Id)
        && Method.Equals(other.Method)
        && Destination.Equals(other.Destination)
        && Headers.Equals(other.Headers)
        && Equals(Content, other.Content)
        && Bounds.Equals(other.Bounds)
        && ResolvedAddresses.SequenceEqual(other.ResolvedAddresses);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Id);
        hash.Add(Method);
        hash.Add(Destination);
        hash.Add(Headers);
        hash.Add(Content);
        hash.Add(Bounds);
        foreach (var address in ResolvedAddresses)
        {
            hash.Add(address);
        }

        return hash.ToHashCode();
    }
}
