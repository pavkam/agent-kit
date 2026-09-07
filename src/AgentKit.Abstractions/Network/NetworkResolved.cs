// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The destination resolved to one or more addresses that satisfy the configured destination policy.</summary>
public sealed record NetworkResolved: NetworkResolutionResult
{
    /// <summary>Initializes a new instance of the <see cref="NetworkResolved"/> record.</summary>
    /// <param name="addresses">The non-empty, ordered set of resolved addresses.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="addresses"/> is a default, uninitialized array, or is empty.
    /// </exception>
    public NetworkResolved(ImmutableArray<NetworkAddress> addresses)
    {
        ArgumentException.ThrowIfDefault(addresses);
        if (addresses.IsEmpty)
        {
            throw new ArgumentException("At least one resolved address is required.", nameof(addresses));
        }

        Addresses = addresses;
    }

    /// <summary>Gets the non-empty, ordered set of resolved addresses.</summary>
    public ImmutableArray<NetworkAddress> Addresses { get; init; }

    /// <inheritdoc/>
    public bool Equals(NetworkResolved? other) => other is not null && Addresses.SequenceEqual(other.Addresses);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var address in Addresses)
        {
            hash.Add(address);
        }

        return hash.ToHashCode();
    }
}
