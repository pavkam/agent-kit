// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Describes one configured trusted issuer and the mapping version captured by resolutions.</summary>
public sealed record IdentityIssuerDescriptor
{
    /// <summary>Initializes an issuer descriptor.</summary>
    /// <param name="id">The stable non-default issuer key.</param>
    /// <param name="version">The version of its trusted subject and tenant mapping.</param>
    /// <exception cref="ArgumentException"><paramref name="id"/> is default or blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="version"/> is not positive.</exception>
    public IdentityIssuerDescriptor(IdentityIssuerId id, IdentityVersion version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id.Value, nameof(id));
        ArgumentOutOfRangeException.ThrowIfLessThan(version.Value, 1, nameof(version));
        Id = id;
        Version = version;
    }

    /// <summary>Gets the stable issuer key.</summary>
    public IdentityIssuerId Id { get; }
    /// <summary>Gets the trusted mapping version.</summary>
    public IdentityVersion Version { get; }
}
