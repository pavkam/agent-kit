// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>References one exact secret-free credential-profile publication.</summary>
/// <remarks>The reference contains no credential and performs no source lookup or authority grant.</remarks>
public sealed record ProviderCredentialProfileReference
{
    /// <summary>Initializes an exact credential-profile reference.</summary>
    /// <param name="key">The nondefault credential-profile key.</param>
    /// <param name="version">The positive credential-profile version.</param>
    /// <exception cref="ArgumentOutOfRangeException">A supplied key or version is default.</exception>
    public ProviderCredentialProfileReference(ProviderCredentialProfileKey key, ProviderCredentialProfileVersion version)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(key, default);
        ArgumentOutOfRangeException.ThrowIfEqual(version, default);
        Key = key;
        Version = version;
    }

    /// <summary>Gets the selected credential-profile family.</summary>
    /// <value>A nondefault key.</value>
    public ProviderCredentialProfileKey Key { get; }

    /// <summary>Gets the exact selected publication version.</summary>
    /// <value>A positive version.</value>
    public ProviderCredentialProfileVersion Version { get; }
}
