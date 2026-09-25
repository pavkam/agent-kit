// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client;

/// <summary>Builds MCP open-request operation evidence from tool discovery input.</summary>
internal static class McpDiscoveryOperationContext
{
    internal static ProtectedSemanticOperationContext FromDiscovery(ToolDiscoveryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var correlation = new InRunOperationCorrelation(
            new OperationId(Guid.Parse("55555555-5555-5555-5555-555555555555")),
            request.RunId,
            turnId: null);
        return new ProtectedSemanticOperationContext(
            request.AgentId,
            request.SessionId,
            conversationId: null,
            request.Identity,
            correlation,
            request.Authorization);
    }

    internal static ProtectedSemanticOperationContext FromContextRequest(ContextContributionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var correlation = new InRunOperationCorrelation(
            new OperationId(request.ModelRequestId.Value),
            request.RunId,
            request.TurnId);
        return new ProtectedSemanticOperationContext(
            request.Agent.Id,
            request.SessionId,
            request.ConversationId,
            request.Identity,
            correlation,
            request.Authorization);
    }
}
