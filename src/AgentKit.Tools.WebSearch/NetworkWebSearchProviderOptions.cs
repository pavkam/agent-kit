// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.WebSearch;

/// <summary>Configures the first-party network-backed web search provider.</summary>
/// <remarks>
/// The provider sends only through <see cref="INetworkTransport"/> after resolving through
/// <see cref="INetworkNameResolver"/>; no option selects a raw HTTP client, proxy, or redirect policy. Redirects are
/// never followed because the search endpoint is one exact configured destination.
/// </remarks>
public sealed class NetworkWebSearchProviderOptions
{
    /// <summary>Gets or sets the required HTTPS search endpoint without user information or fragment.</summary>
    /// <value>A non-null absolute HTTPS URI; no default is supplied.</value>
    public Uri? Endpoint { get; set; }

    /// <summary>Gets or sets the stable provider identity exposed to the search tool.</summary>
    public ProviderId ProviderId { get; set; } = new("agentkit.network-web-search");

    /// <summary>Gets or sets the maximum response body size accepted from the search service.</summary>
    /// <value>A positive byte count enforced while the body streams.</value>
    public int MaximumResponseBytes { get; set; } = 262_144;

    /// <summary>Gets or sets the resolution and connect timeout applied to each attempt.</summary>
    /// <value>A positive duration, further clamped to the time remaining before the attempt deadline.</value>
    public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Gets or sets the data classification declared for every outbound search query.</summary>
    /// <value>A defined classification; the default is <see cref="NetworkDataClassification.Confidential"/> because queries reveal user intent.</value>
    public NetworkDataClassification Classification { get; set; } = NetworkDataClassification.Confidential;
}
