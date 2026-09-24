// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Resolves keyed MCP transport factories registered in composition.</summary>
public sealed class DefaultMcpTransportFactoryCatalog(IServiceProvider services): IMcpTransportFactoryCatalog
{
    private readonly IServiceProvider _services = services;

    /// <inheritdoc/>
    public ValueTask<McpTransportFactoryResolution> ResolveAsync(
        McpTransportProfile transportProfile,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transportProfile);
        cancellationToken.ThrowIfCancellationRequested();
        var profileType = transportProfile.GetType();
        var factories = _services.GetServices<IMcpTransportFactory>();
        foreach (var factory in factories)
        {
            if (factory.TransportProfileType == profileType)
            {
                return ValueTask.FromResult<McpTransportFactoryResolution>(new McpTransportFactoryResolved(factory));
            }
        }

        return ValueTask.FromResult<McpTransportFactoryResolution>(new McpTransportFactoryNotFound(profileType));
    }
}
