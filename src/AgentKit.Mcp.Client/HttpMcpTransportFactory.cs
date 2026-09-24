// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client;

using Microsoft.Extensions.DependencyInjection;

using ModelContextProtocol.Client;

/// <summary>Opens HTTP MCP transports after connect authorization and optional OAuth token binding.</summary>
/// <remarks>
/// Per-request JSON-RPC frames continue through the official
/// <see cref="HttpClientTransport"/>. Connect-time authorization uses the
/// captured operation context; applications should register
/// <see cref="INetworkTransport"/> for environments that require per-hop
/// network grant enforcement on custom HTTP handlers.
/// </remarks>
public sealed class HttpMcpTransportFactory(
    ISecurityAuthoritySelector securityAuthorities,
    ISecurityGrantStore grantStore,
    ISecurityAuditDispatcher audit,
    IIdentifierGenerator<SecurityRequestId> securityRequestIds,
    IIdentifierGenerator<SecurityAuditRecordId> auditRecordIds,
    IServiceProvider services,
    TimeProvider timeProvider,
    ILoggerFactory loggerFactory): IMcpTransportFactory
{
    /// <inheritdoc/>
    public Type TransportProfileType => typeof(McpHttpTransportProfile);

    /// <inheritdoc/>
    public async ValueTask<McpTransportOpenResult> OpenAsync(
        McpTransportOpenRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Endpoint.Transport is not McpHttpTransportProfile httpProfile)
        {
            return new McpTransportOpenFailed("The HTTP transport factory received a non-HTTP endpoint profile.");
        }

        var operation = request.SessionOpenRequest.Operation;
        var grant = await McpClientSecurityOperations.AuthorizeConnectAsync(
            operation,
            request.Endpoint,
            securityAuthorities,
            grantStore,
            audit,
            securityRequestIds,
            auditRecordIds,
            timeProvider,
            cancellationToken).ConfigureAwait(false);
        if (grant is null)
        {
            return new McpTransportOpenDenied("HTTP MCP connect denied by security authority.");
        }

        _ = await McpClientSecurityOperations.ConsumeGrantAsync(grant, grantStore, cancellationToken)
            .ConfigureAwait(false);

        var additionalHeaders = new Dictionary<string, string>(StringComparer.Ordinal);
        if (request.Endpoint.Authentication is { } authentication)
        {
            var tokenProvider = services.GetKeyedService<IOAuthAccessTokenProvider>(authentication.CredentialProfileKey);
            if (tokenProvider is null)
            {
                return new McpTransportOpenFailed(
                    $"No OAuth access token provider is registered for credential profile '{authentication.CredentialProfileKey}'.");
            }

            var token = await tokenProvider.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(token.AccessToken))
            {
                return new McpTransportOpenFailed("OAuth access token acquisition returned no token.");
            }

            additionalHeaders["Authorization"] = $"Bearer {token.AccessToken}";
        }

        var options = new HttpClientTransportOptions
        {
            Endpoint = httpProfile.Endpoint,
            Name = request.Endpoint.Key.Value,
            AdditionalHeaders = additionalHeaders,
        };
        var transport = new HttpClientTransport(options, loggerFactory: loggerFactory);
        return new McpTransportOpened(new SdkMcpClientTransport(transport));
    }
}
