// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network;

using System.Net;

/// <summary>Identifies one route-partitioned connection pool for network sends.</summary>
internal readonly record struct NetworkConnectionPartitionKey
{
    internal NetworkConnectionPartitionKey(
        NetworkDestination destination,
        IPAddress peerAddress,
        NetworkProxyDescriptor proxy,
        NetworkTlsPolicy tlsPolicy,
        NetworkDecompressionPolicy decompressionPolicy)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(proxy);
        Scheme = destination.Scheme;
        Host = destination.Host.Value;
        Port = destination.Port;
        Route = destination.Route.Value;
        PeerAddress = peerAddress.ToString();
        Proxy = proxy.UseProxy ? proxy.ProxyUri!.ToString() : string.Empty;
        CredentialAudience = proxy.CredentialAudience?.Value ?? string.Empty;
        TlsPolicy = tlsPolicy;
        DecompressionPolicy = decompressionPolicy;
    }

    internal string Scheme { get; }
    internal string Host { get; }
    internal int Port { get; }
    internal string Route { get; }
    internal string PeerAddress { get; }
    internal string Proxy { get; }
    internal string CredentialAudience { get; }
    internal NetworkTlsPolicy TlsPolicy { get; }
    internal NetworkDecompressionPolicy DecompressionPolicy { get; }
}
