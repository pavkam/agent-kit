// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>One registered network profile was selected.</summary>
public sealed record NetworkProfileSelected: NetworkProfileSelectionResult
{
    /// <summary>Initializes a successful profile selection.</summary>
    /// <param name="key">The selected profile key.</param>
    /// <param name="resolver">The resolver registered under the key.</param>
    /// <param name="transport">The transport registered under the key.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    public NetworkProfileSelected(
        NetworkProfileKey key,
        INetworkNameResolver resolver,
        INetworkTransport transport)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(key, default);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(transport);
        Key = key;
        Resolver = resolver;
        Transport = transport;
    }

    /// <summary>Gets the selected profile key.</summary>
    public NetworkProfileKey Key { get; init; }

    /// <summary>Gets the resolver registered under the key.</summary>
    public INetworkNameResolver Resolver { get; init; }

    /// <summary>Gets the transport registered under the key.</summary>
    public INetworkTransport Transport { get; init; }
}
