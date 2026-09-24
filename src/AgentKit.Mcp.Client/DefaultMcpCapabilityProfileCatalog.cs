// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Resolves neutral capability references to MCP-owned client profiles.</summary>
internal sealed class DefaultMcpCapabilityProfileCatalog: IMcpCapabilityProfileCatalog
{
    private readonly IServiceProvider _provider;

    /// <summary>Initializes the default capability profile catalog.</summary>
    /// <param name="provider">The host provider used to read keyed profile registrations.</param>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is null.</exception>
    public DefaultMcpCapabilityProfileCatalog(IServiceProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        _provider = provider;
    }

    /// <inheritdoc/>
    public ValueTask<McpCapabilityProfileResolution> ResolveAsync(
        AgentCapabilityReference capability,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(capability);
        _ = cancellationToken;
        if (capability.CapabilityId != McpCapabilityIds.Client)
        {
            return ValueTask.FromResult<McpCapabilityProfileResolution>(
                new McpCapabilityProfileNotSupported(capability.CapabilityId));
        }

        var profile = _provider.GetKeyedService<McpCapabilityProfile>(capability.ProfileId.Value);
        return profile is null
            ? ValueTask.FromResult<McpCapabilityProfileResolution>(
                new McpCapabilityProfileNotFound(capability.ProfileId))
            : profile.ProfileId != capability.ProfileId
                ? throw new InvalidOperationException("The keyed MCP capability profile does not match its registration key.")
                : ValueTask.FromResult<McpCapabilityProfileResolution>(new McpCapabilityProfileResolved(profile));
    }
}
