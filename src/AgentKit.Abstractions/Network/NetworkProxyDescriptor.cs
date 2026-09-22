// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Declares an explicit outbound HTTP proxy route for one network profile.</summary>
/// <remarks>
/// Proxy selection is a capability, not an authority bypass. Credential values
/// remain outside security requests and audit records.
/// </remarks>
public sealed record NetworkProxyDescriptor
{
    /// <summary>Initializes a direct connection profile with no proxy.</summary>
    public NetworkProxyDescriptor() => UseProxy = false;

    /// <summary>Initializes a proxy-backed route.</summary>
    /// <param name="proxyUri">The absolute proxy URI including scheme and port.</param>
    /// <param name="credentialAudience">The optional credential audience bound to proxy authentication.</param>
    /// <exception cref="ArgumentException"><paramref name="proxyUri"/> is not absolute.</exception>
    public NetworkProxyDescriptor(Uri proxyUri, ComponentId? credentialAudience = null)
    {
        ArgumentNullException.ThrowIfNull(proxyUri);
        if (!proxyUri.IsAbsoluteUri)
        {
            throw new ArgumentException("The proxy URI must be absolute.", nameof(proxyUri));
        }

        UseProxy = true;
        ProxyUri = proxyUri;
        CredentialAudience = credentialAudience;
    }

    /// <summary>Gets whether sends use an explicit proxy.</summary>
    public bool UseProxy { get; init; }

    /// <summary>Gets the absolute proxy URI when <see cref="UseProxy"/> is true.</summary>
    public Uri? ProxyUri { get; init; }

    /// <summary>Gets the credential audience used for proxy authentication, when configured.</summary>
    public ComponentId? CredentialAudience { get; init; }
}
