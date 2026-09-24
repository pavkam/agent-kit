// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client;

/// <summary>Authorizes MCP client connect and request effects through the captured security authority.</summary>
internal static class McpClientSecurityOperations
{
    internal static readonly ComponentId SessionAudience = new("agentkit.mcp.client.session");
    internal static readonly ComponentId TransportNetworkAudience = new("agentkit.mcp.client.transport.http");
    internal static readonly ComponentId TransportProcessAudience = new("agentkit.mcp.client.transport.stdio");

    internal static async ValueTask<SecurityGrant?> AuthorizeConnectAsync(
        ProtectedSemanticOperationContext operation,
        McpEndpoint endpoint,
        ISecurityAuthoritySelector authoritySelector,
        ISecurityGrantStore grantStore,
        ISecurityAuditDispatcher audit,
        IIdentifierGenerator<SecurityRequestId> requestIds,
        IIdentifierGenerator<SecurityAuditRecordId> auditRecordIds,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(authoritySelector);
        ArgumentNullException.ThrowIfNull(grantStore);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(requestIds);
        ArgumentNullException.ThrowIfNull(auditRecordIds);
        ArgumentNullException.ThrowIfNull(timeProvider);

        var resources = BuildConnectResources(endpoint);
        var request = CreateRequest(
            operation,
            requestIds.Create(),
            SessionAudience,
            endpoint.Transport is McpHttpTransportProfile ? SecurityOperationKind.Network : SecurityOperationKind.Process,
            endpoint.Transport is McpHttpTransportProfile ? SecurityEffect.Egress : SecurityEffect.Execute,
            resources,
            timeProvider.GetUtcNow().AddMinutes(5));
        return await AuthorizeAndRegisterAsync(
            request,
            operation.Authorization,
            authoritySelector,
            grantStore,
            cancellationToken).ConfigureAwait(false);
    }

    internal static async ValueTask<SecurityGrant?> AuthorizeMcpRequestAsync(
        McpRequest mcpRequest,
        ISecurityAuthoritySelector authoritySelector,
        ISecurityGrantStore grantStore,
        IIdentifierGenerator<SecurityRequestId> requestIds,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mcpRequest);
        var operation = mcpRequest.Operation;
        var resources = ImmutableArray.Create(
            new ProtectedResource(
                ProtectedResourceKind.ApplicationState,
                $"mcp:session:{operation.AgentId.Value}"));
        var kind = mcpRequest switch
        {
            McpToolsCallRequest => SecurityOperationKind.StateMutation,
            _ => SecurityOperationKind.StateRead,
        };
        var effect = kind == SecurityOperationKind.StateMutation
            ? SecurityEffect.Mutate
            : SecurityEffect.Observe;
        var request = CreateRequest(
            operation,
            requestIds.Create(),
            SessionAudience,
            kind,
            effect,
            resources,
            timeProvider.GetUtcNow().AddMinutes(5),
            mcpRequest.ToolCallId);
        return await AuthorizeAndRegisterAsync(
            request,
            operation.Authorization,
            authoritySelector,
            grantStore,
            cancellationToken).ConfigureAwait(false);
    }

    internal static ValueTask<GrantConsumptionResult> ConsumeGrantAsync(
        SecurityGrant grant,
        ISecurityGrantStore grantStore,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(grant);
        ArgumentNullException.ThrowIfNull(grantStore);
        var enforcement = CreateEnforcement(grant);
        return grantStore.ValidateAndConsumeAsync(grant, enforcement, cancellationToken);
    }

    private static async ValueTask<SecurityGrant?> AuthorizeAndRegisterAsync(
        SecurityRequest request,
        SecurityAuthorizationContext authorization,
        ISecurityAuthoritySelector authoritySelector,
        ISecurityGrantStore grantStore,
        CancellationToken cancellationToken)
    {
        var selection = await authoritySelector.SelectAsync(authorization, cancellationToken).ConfigureAwait(false);
        if (selection is not SecurityAuthoritySelected selected)
        {
            return null;
        }

        var decision = await selected.Authority.AuthorizeAsync(request, cancellationToken).ConfigureAwait(false);
        if (decision is not SecurityAllowed allowed)
        {
            return null;
        }

        await grantStore.RegisterAsync(allowed.Grant, cancellationToken).ConfigureAwait(false);
        return allowed.Grant;
    }

    private static SecurityRequest CreateRequest(
        ProtectedSemanticOperationContext operation,
        SecurityRequestId requestId,
        ComponentId audience,
        SecurityOperationKind kind,
        SecurityEffect effect,
        ImmutableArray<ProtectedResource> resources,
        DateTimeOffset deadline,
        ToolCallId? toolCallId = null) =>
        new(
            requestId,
            operation.Authorization.Scope,
            toolCallId,
            operation.Identity,
            operation.Authorization,
            audience,
            kind,
            effect,
            resources,
            new InputFingerprint("mcp"),
            deadline);

    private static ImmutableArray<ProtectedResource> BuildConnectResources(McpEndpoint endpoint)
    {
        var identifier = endpoint.Transport switch
        {
            McpStdioTransportProfile stdio =>
                $"stdio:{stdio.Command}",
            McpHttpTransportProfile http =>
                http.Endpoint.GetLeftPart(UriPartial.Authority),
            _ => endpoint.Key.Value,
        };
        var kind = endpoint.Transport is McpHttpTransportProfile
            ? ProtectedResourceKind.NetworkEndpoint
            : ProtectedResourceKind.Process;
        return [new ProtectedResource(kind, identifier)];
    }

    private static SecurityEnforcementRequest CreateEnforcement(SecurityGrant grant) =>
        new(
            grant.Scope,
            grant.Identity,
            grant.Audience,
            grant.Kind,
            grant.Effect,
            grant.Resources,
            grant.InputFingerprint,
            grant.RevocationVersion);
}
