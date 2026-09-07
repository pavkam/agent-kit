// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Net;

/// <summary>One resolved network address, with the instant it was resolved and when it expires.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization. A
/// transport connects only to an address returned by a prior, still-valid
/// resolution; it never lets the operating system re-resolve a hostname at
/// connect time, since doing so would reopen the DNS-rebinding window this
/// contract exists to close.
/// </remarks>
public sealed record NetworkAddress
{
    /// <summary>Initializes a new instance of the <see cref="NetworkAddress"/> record.</summary>
    /// <param name="address">The resolved IP address.</param>
    /// <param name="resolvedAt">The instant this address was resolved.</param>
    /// <param name="expiresAt">The instant after which this resolution must not be reused.</param>
    /// <exception cref="ArgumentNullException"><paramref name="address"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="expiresAt"/> is not later than <paramref name="resolvedAt"/>.</exception>
    public NetworkAddress(IPAddress address, DateTimeOffset resolvedAt, DateTimeOffset expiresAt)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (expiresAt <= resolvedAt)
        {
            throw new ArgumentException("Value must be later than resolvedAt.", nameof(expiresAt));
        }

        Address = address;
        ResolvedAt = resolvedAt;
        ExpiresAt = expiresAt;
    }

    /// <summary>Gets the resolved IP address.</summary>
    public IPAddress Address { get; init; }

    /// <summary>Gets the instant this address was resolved.</summary>
    public DateTimeOffset ResolvedAt { get; init; }

    /// <summary>Gets the instant after which this resolution must not be reused.</summary>
    public DateTimeOffset ExpiresAt { get; init; }
}
