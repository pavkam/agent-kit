// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>References one exact endpoint-profile publication.</summary>
/// <remarks>The reference is selection evidence only. Profile publication and runtime selection perform lookup and availability validation.</remarks>
public sealed record ProviderEndpointProfileReference
{
    /// <summary>Initializes an exact endpoint-profile reference.</summary>
    /// <param name="key">The nondefault endpoint-profile key.</param>
    /// <param name="version">The positive endpoint-profile version.</param>
    /// <exception cref="ArgumentOutOfRangeException">A supplied key or version is default.</exception>
    public ProviderEndpointProfileReference(ProviderEndpointProfileKey key, ProviderEndpointProfileVersion version)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(key, default);
        ArgumentOutOfRangeException.ThrowIfEqual(version, default);
        Key = key;
        Version = version;
    }

    /// <summary>Gets the selected endpoint-profile family.</summary>
    /// <value>A nondefault key.</value>
    public ProviderEndpointProfileKey Key { get; }

    /// <summary>Gets the exact selected publication version.</summary>
    /// <value>A positive version.</value>
    public ProviderEndpointProfileVersion Version { get; }
}
