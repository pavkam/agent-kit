// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client;

/// <summary>Discovers remote MCP tools from one configured endpoint through an independent session capture.</summary>
public sealed class McpToolProvider(
    ToolSourceId sourceId,
    McpEndpointKey endpointKey,
    IMcpClientSessionFactory sessionFactory,
    IMcpEndpointCatalog endpointCatalog,
    IMcpCapabilityProfileCatalog profileCatalog,
    CapabilityProfileId capabilityProfileId,
    IIdentifierGenerator<McpRequestId> requestIds,
    TimeProvider timeProvider): IToolProvider
{
    private readonly McpEndpointKey _endpointKey = endpointKey;
    private readonly IMcpClientSessionFactory _sessionFactory = sessionFactory;
    private readonly IMcpEndpointCatalog _endpointCatalog = endpointCatalog;
    private readonly IMcpCapabilityProfileCatalog _profileCatalog = profileCatalog;
    private readonly CapabilityProfileId _capabilityProfileId = capabilityProfileId;
    private readonly IIdentifierGenerator<McpRequestId> _requestIds = requestIds;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc/>
    public ToolSourceId SourceId { get; } = sourceId;

    /// <inheritdoc/>
    public async ValueTask<IToolProviderCapture> DiscoverAsync(
        ToolDiscoveryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var endpointResolution = await _endpointCatalog.ResolveAsync(_endpointKey, cancellationToken).ConfigureAwait(false);
        if (endpointResolution is not McpEndpointResolved endpointResolved)
        {
            throw new InvalidOperationException($"MCP endpoint '{_endpointKey.Value}' is not registered.");
        }

        var profileResolution = await _profileCatalog.ResolveAsync(
            new AgentCapabilityReference(McpCapabilityIds.Client, _capabilityProfileId),
            cancellationToken).ConfigureAwait(false);
        if (profileResolution is not McpCapabilityProfileResolved profileResolved)
        {
            throw new InvalidOperationException($"MCP capability profile '{_capabilityProfileId.Value}' is not registered.");
        }

        var openRequest = new McpClientOpenRequest(
            profileResolved.Profile,
            endpointResolved.Endpoint,
            McpDiscoveryOperationContext.FromDiscovery(request));
        var session = await _sessionFactory.OpenAsync(openRequest, cancellationToken).ConfigureAwait(false);
        _ = await session.InitializeAsync(cancellationToken).ConfigureAwait(false);
        var catalog = await session.GetCatalogAsync(cancellationToken).ConfigureAwait(false);
        var snapshot = new ToolProviderSnapshot(
            SourceId,
            new ToolSourceVersion(catalog.Version.Value.ToString(CultureInfo.InvariantCulture)),
            catalog.Tools);
        var invokers = catalog.Tools.ToDictionary(
            static tool => new ToolIdentity(tool.Id, tool.Version),
            tool => (IToolInvoker) new McpToolInvoker(
                session,
                tool,
                _requestIds,
                McpDiscoveryOperationContext.FromDiscovery(request)));
        return new McpToolProviderCapture(snapshot, invokers, session, _timeProvider);
    }
}
