// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.WebSearch;

/// <summary>Configures the first-party network-backed web search provider.</summary>
public sealed class NetworkWebSearchProviderOptions
{
    /// <summary>Gets or sets the required HTTPS search endpoint without query parameters.</summary>
    /// <value>A non-null absolute URI; no default is supplied.</value>
    public Uri? Endpoint { get; set; }

    /// <summary>Gets or sets the stable provider identity exposed to the search tool.</summary>
    public ProviderId ProviderId { get; set; } = new("agentkit.network-web-search");

    /// <summary>Gets or sets the maximum response body size accepted from the search service.</summary>
    public int MaximumResponseBytes { get; set; } = 262_144;

    /// <summary>Gets or sets the connect timeout applied to each attempt.</summary>
    public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(10);
}
