// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client;

using Microsoft.Extensions.Logging;

/// <summary>Opens MCP client sessions using registered transports and security collaborators.</summary>
internal sealed class McpClientSessionFactory(
    IMcpTransportFactoryCatalog transports,
    ISecurityAuthoritySelector securityAuthorities,
    ISecurityGrantStore grants,
    ISecurityAuditDispatcher audit,
    IIdentifierGenerator<McpSessionId> sessionIds,
    IIdentifierGenerator<SecurityRequestId> securityRequestIds,
    IIdentifierGenerator<SecurityAuditRecordId> auditRecordIds,
    TimeProvider timeProvider,
    McpClientOptionsSnapshot options,
    ILoggerFactory loggerFactory): IMcpClientSessionFactory
{
    private readonly IMcpTransportFactoryCatalog _transports = transports;
    private readonly ISecurityAuthoritySelector _securityAuthorities = securityAuthorities;
    private readonly ISecurityGrantStore _grants = grants;
    private readonly ISecurityAuditDispatcher _audit = audit;
    private readonly IIdentifierGenerator<McpSessionId> _sessionIds = sessionIds;
    private readonly IIdentifierGenerator<SecurityRequestId> _securityRequestIds = securityRequestIds;
    private readonly IIdentifierGenerator<SecurityAuditRecordId> _auditRecordIds = auditRecordIds;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly McpClientOptionsSnapshot _options = options;
    private readonly ILoggerFactory _loggerFactory = loggerFactory;

    /// <inheritdoc/>
    public ValueTask<IMcpClientSession> OpenAsync(McpClientOpenRequest request, CancellationToken cancellationToken) =>
        McpClientSession.OpenAsync(
            request,
            _transports,
            _securityAuthorities,
            _grants,
            _audit,
            _sessionIds,
            _securityRequestIds,
            _auditRecordIds,
            _timeProvider,
            _options,
            new ToolSourceId($"mcp.endpoint.{request.Endpoint.Key.Value}"),
            _loggerFactory,
            cancellationToken);
}
