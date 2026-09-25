// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client;

/// <summary>Shared MCP session open helpers for context contributors.</summary>
internal static class McpContextSessionFactory
{
    internal static async ValueTask<IMcpClientSession?> OpenReadOnlySessionAsync(
        McpEndpointContextBinding binding,
        ContextContributionRequest request,
        IMcpClientSessionFactory sessionFactory,
        IMcpEndpointCatalog endpointCatalog,
        IMcpCapabilityProfileCatalog profileCatalog,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(sessionFactory);
        ArgumentNullException.ThrowIfNull(endpointCatalog);
        ArgumentNullException.ThrowIfNull(profileCatalog);
        var endpointResolution = await endpointCatalog.ResolveAsync(binding.EndpointKey, cancellationToken).ConfigureAwait(false);
        if (endpointResolution is not McpEndpointResolved endpointResolved)
        {
            return null;
        }

        var profileResolution = await profileCatalog.ResolveAsync(
            new AgentCapabilityReference(McpCapabilityIds.Client, binding.CapabilityProfileId),
            cancellationToken).ConfigureAwait(false);
        if (profileResolution is not McpCapabilityProfileResolved profileResolved)
        {
            return null;
        }

        var operation = McpDiscoveryOperationContext.FromContextRequest(request);
        var openRequest = new McpClientOpenRequest(profileResolved.Profile, endpointResolved.Endpoint, operation);
        var session = await sessionFactory.OpenAsync(openRequest, cancellationToken).ConfigureAwait(false);
        _ = await session.InitializeAsync(cancellationToken).ConfigureAwait(false);
        return session;
    }
}
