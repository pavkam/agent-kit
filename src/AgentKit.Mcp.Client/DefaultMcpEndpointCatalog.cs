// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Resolves explicitly registered MCP endpoints by semantic key.</summary>
internal sealed class DefaultMcpEndpointCatalog: IMcpEndpointCatalog
{
    private readonly IServiceProvider _provider;

    /// <summary>Initializes the default endpoint catalog.</summary>
    /// <param name="provider">The host provider used to read keyed endpoint registrations.</param>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is null.</exception>
    public DefaultMcpEndpointCatalog(IServiceProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        _provider = provider;
    }

    /// <inheritdoc/>
    public ValueTask<McpEndpointResolution> ResolveAsync(
        McpEndpointKey key,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        _ = cancellationToken;
        var endpoint = _provider.GetKeyedService<McpEndpoint>(key.Value);
        return endpoint is null
            ? ValueTask.FromResult<McpEndpointResolution>(new McpEndpointNotFound(key))
            : endpoint.Key != key
                ? throw new InvalidOperationException("The keyed MCP endpoint does not match its registration key.")
                : ValueTask.FromResult<McpEndpointResolution>(new McpEndpointResolved(endpoint));
    }
}
