// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Binds one operation to exact endpoint and credential-profile references.</summary>
/// <remarks>This immutable selection does not resolve profiles, verify their compatibility, expose a secret, or grant egress authority.</remarks>
public sealed record ProviderOperationBinding
{
    /// <summary>Initializes one captured operation binding.</summary>
    /// <param name="endpoint">The non-null exact endpoint-profile reference.</param>
    /// <param name="credential">The non-null exact credential-profile reference.</param>
    /// <exception cref="ArgumentNullException">A reference is null.</exception>
    public ProviderOperationBinding(ProviderEndpointProfileReference endpoint, ProviderCredentialProfileReference credential)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(credential);
        Endpoint = endpoint;
        Credential = credential;
    }

    /// <summary>Gets the endpoint-profile selection.</summary>
    /// <value>A non-null exact reference.</value>
    public ProviderEndpointProfileReference Endpoint { get; }

    /// <summary>Gets the credential-profile selection.</summary>
    /// <value>A non-null exact secret-free reference.</value>
    public ProviderCredentialProfileReference Credential { get; }
}
