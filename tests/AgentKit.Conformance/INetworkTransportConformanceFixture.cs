// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

/// <summary>Adapter-specific setup for shared network transport contract scenarios.</summary>
public interface INetworkTransportConformanceFixture: IAsyncDisposable
{
    /// <summary>Gets the transport under test.</summary>
    public INetworkTransport Transport { get; }

    /// <summary>Gets the resolver paired with the transport when redirect scenarios require it.</summary>
    public INetworkNameResolver? Resolver { get; }

    /// <summary>Configures audit to fail closed before host effects.</summary>
    public void UseRejectingAudit();

    /// <summary>Builds one authorized send request to the fixture destination.</summary>
    /// <param name="maximumResponseBytes">The response bound to embed in the request.</param>
    /// <returns>A complete request with a fresh grant.</returns>
    public NetworkRequest CreateAuthorizedSend(long maximumResponseBytes = 1024);
}
