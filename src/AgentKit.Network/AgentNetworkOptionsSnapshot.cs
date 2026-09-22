// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network;

/// <summary>Immutable validated options for one keyed network profile.</summary>
internal sealed record AgentNetworkOptionsSnapshot
{
    internal AgentNetworkOptionsSnapshot(
        NetworkProfileKey profileKey,
        NetworkProfileVersion profileVersion,
        NetworkDestinationPolicy destinationPolicy,
        TimeSpan addressResolutionLifetime,
        int maximumResponseHeaderKilobytes,
        NetworkProxyDescriptor proxy,
        NetworkTlsPolicy tlsPolicy,
        NetworkDecompressionPolicy decompressionPolicy)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(profileKey, default);
        ArgumentNullException.ThrowIfNull(destinationPolicy);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(addressResolutionLifetime, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumResponseHeaderKilobytes);
        ArgumentNullException.ThrowIfNull(proxy);
        ProfileKey = profileKey;
        ProfileVersion = profileVersion;
        DestinationPolicy = destinationPolicy;
        AddressResolutionLifetime = addressResolutionLifetime;
        MaximumResponseHeaderKilobytes = maximumResponseHeaderKilobytes;
        Proxy = proxy;
        TlsPolicy = tlsPolicy;
        DecompressionPolicy = decompressionPolicy;
    }

    internal NetworkProfileKey ProfileKey { get; }
    internal NetworkProfileVersion ProfileVersion { get; }
    internal NetworkDestinationPolicy DestinationPolicy { get; }
    internal TimeSpan AddressResolutionLifetime { get; }
    internal int MaximumResponseHeaderKilobytes { get; }
    internal NetworkProxyDescriptor Proxy { get; }
    internal NetworkTlsPolicy TlsPolicy { get; }
    internal NetworkDecompressionPolicy DecompressionPolicy { get; }
}
