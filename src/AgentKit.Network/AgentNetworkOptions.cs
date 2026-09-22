// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network;

/// <summary>Configures structural network policy and resolution freshness.</summary>
public sealed class AgentNetworkOptions
{
    /// <summary>Gets or sets the independently enforced destination policy.</summary>
    public NetworkDestinationPolicy DestinationPolicy { get; set; } = NetworkDestinationPolicy.Default;

    /// <summary>Gets or sets how long resolved addresses remain eligible for an authorized connection.</summary>
    public TimeSpan AddressResolutionLifetime { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>Gets or sets the maximum aggregate HTTP response-header size in kibibytes.</summary>
    public int MaximumResponseHeaderKilobytes { get; set; } = 64;

    /// <summary>Gets or sets the explicit proxy route for this profile.</summary>
    public NetworkProxyDescriptor Proxy { get; set; } = new();

    /// <summary>Gets or sets TLS validation behavior for this profile.</summary>
    public NetworkTlsPolicy TlsPolicy { get; set; } = NetworkTlsPolicy.PlatformDefault;

    /// <summary>Gets or sets automatic response decompression behavior.</summary>
    public NetworkDecompressionPolicy DecompressionPolicy { get; set; } = NetworkDecompressionPolicy.DenyAutomatic;
}
