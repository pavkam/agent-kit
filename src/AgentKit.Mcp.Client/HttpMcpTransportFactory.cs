// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client;

using System.Net.Http;

using Microsoft.Extensions.DependencyInjection;

using ModelContextProtocol.Client;

/// <summary>Opens HTTP MCP transports through the protected network boundary after connect authorization and optional OAuth token binding.</summary>
/// <remarks>
/// <para>
/// The official <see cref="HttpClientTransport"/> still owns Streamable HTTP and legacy SSE protocol behavior, but it is
/// handed an <see cref="HttpClient"/> over <see cref="NetworkMcpHttpHandler"/>, so every request, event-stream read,
/// and session deletion resolves and sends through <see cref="INetworkNameResolver"/> and
/// <see cref="INetworkTransport"/> under per-exchange resolution and send grants. The SDK never creates a socket,
/// follows a redirect, or reaches an origin other than the configured endpoint's. Missing authority, enforcement, or
/// audit refuses the exchange; there is no fallback to an unrestricted client.
/// </para>
/// <para>
/// The connect grant authorizes the connection effect and is consumed before the transport exists; it never
/// substitutes for the per-exchange network grants, and a successful OAuth token acquisition authorizes neither.
/// The access token travels only as a request header inside the network request, bound by hash alone.
/// </para>
/// </remarks>
internal sealed class HttpMcpTransportFactory(
    ISecurityAuthoritySelector securityAuthorities,
    ISecurityGrantStore grantStore,
    ISecurityAuditDispatcher audit,
    IIdentifierGenerator<SecurityRequestId> securityRequestIds,
    IIdentifierGenerator<SecurityAuditRecordId> auditRecordIds,
    IIdentifierGenerator<SecurityEnforcementIntentId> intentIds,
    IIdentifierGenerator<NetworkOperationId> networkOperationIds,
    INetworkNameResolver resolver,
    INetworkTransport networkTransport,
    McpClientOptionsSnapshot clientOptions,
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

        var consumed = await McpClientSecurityOperations.TryConsumeGrantAsync(
            grant,
            grantStore,
            audit,
            auditRecordIds,
            intentIds,
            timeProvider,
            cancellationToken).ConfigureAwait(false);
        if (!consumed)
        {
            return new McpTransportOpenDenied("HTTP MCP connect grant consumption failed.");
        }

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
            ConnectionTimeout = request.Endpoint.Bounds.HandshakeTimeout,
            AdditionalHeaders = additionalHeaders,
        };
        var handler = new NetworkMcpHttpHandler(
            resolver,
            networkTransport,
            securityAuthorities,
            securityRequestIds,
            networkOperationIds,
            operation,
            request.Endpoint,
            clientOptions,
            timeProvider,
            loggerFactory.CreateLogger<NetworkMcpHttpHandler>());
        var client = new HttpClient(handler, disposeHandler: true) { Timeout = Timeout.InfiniteTimeSpan };
        var transport = new HttpClientTransport(options, client, loggerFactory, ownsHttpClient: true);
        return new McpTransportOpened(new SdkMcpClientTransport(transport));
    }
}
