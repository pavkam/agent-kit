// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network;

/// <summary>Mutable, validated binding options for the built-in real network implementation.</summary>
public sealed class AgentNetworkOptions
{
    /// <summary>
    /// Gets or sets how long a resolved address remains valid for connection
    /// before a fresh resolution is required. Defaults to 60 seconds.
    /// </summary>
    public TimeSpan AddressResolutionLifetime { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>Gets or sets the destination policy the resolver and transport enforce.</summary>
    /// <remarks>See <see cref="NetworkDestinationPolicy"/> for the reduced-scope rationale.</remarks>
    public NetworkDestinationPolicy DestinationPolicy { get; set; } = NetworkDestinationPolicy.Default;
}
