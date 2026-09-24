// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client;

/// <summary>
/// Opens stdio MCP transports through the protected process boundary.
/// </summary>
/// <remarks>
/// <see cref="IProcessHandle"/> currently exposes bounded stdout/stderr reads only and
/// does not surface a bidirectional stdin writer. Until host-access adds that capability,
/// this factory fails composition-time opens with an explicit transport failure rather
/// than pretending stdio MCP is available.
/// </remarks>
public sealed class StdioMcpTransportFactory(
    IProcessExecutorSelector processExecutors,
    ISecurityAuthoritySelector securityAuthorities,
    ISecurityGrantStore grantStore,
    ISecurityAuditDispatcher audit,
    IIdentifierGenerator<SecurityRequestId> securityRequestIds,
    IIdentifierGenerator<SecurityAuditRecordId> auditRecordIds,
    TimeProvider timeProvider): IMcpTransportFactory
{
    /// <inheritdoc/>
    public Type TransportProfileType => typeof(McpStdioTransportProfile);

    /// <inheritdoc/>
    public ValueTask<McpTransportOpenResult> OpenAsync(
        McpTransportOpenRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (request.Endpoint.Transport is not McpStdioTransportProfile)
        {
            return ValueTask.FromResult<McpTransportOpenResult>(
                new McpTransportOpenFailed(
                    "The stdio transport factory received a non-stdio endpoint profile."));
        }

        _ = processExecutors;
        _ = securityAuthorities;
        _ = grantStore;
        _ = audit;
        _ = securityRequestIds;
        _ = auditRecordIds;
        _ = timeProvider;
        return ValueTask.FromResult<McpTransportOpenResult>(
            new McpTransportOpenFailed(
                "Stdio MCP transport requires bidirectional process stdin, which IProcessHandle does not expose yet."));
    }
}
