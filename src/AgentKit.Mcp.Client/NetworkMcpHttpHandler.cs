// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client;

using System.Net.Http;

/// <summary>
/// Carries every HTTP exchange of the official MCP SDK transport through the AgentKit network boundary, so the SDK
/// never opens a socket, resolves DNS, or follows a redirect on its own.
/// </summary>
/// <remarks>
/// <para>
/// The SDK's <c>HttpClientTransport</c> is handed an <see cref="HttpClient"/> over this handler. For each request the
/// handler first refuses anything that is not <c>GET</c>, <c>POST</c>, or <c>DELETE</c> to the configured endpoint's
/// exact origin (scheme, host, and port), so a server-supplied legacy message URL on another origin can never receive
/// the endpoint's credentials. It then freezes the body into immutable bytes and, using the authority captured with
/// the session's <see cref="ProtectedSemanticOperationContext"/>, obtains a resolution grant, resolves through
/// <see cref="INetworkNameResolver"/>, obtains a separate send grant bound to the resolved addresses, method,
/// destination, header-value fingerprints, body fingerprint, bounds, and classification, and sends through
/// <see cref="INetworkTransport"/>. Nothing is resolved, connected, or transmitted before the corresponding grant
/// exists, and the resolver and transport each consume their own grant, so a connect-time or per-MCP-request grant can
/// never stand in for either.
/// </para>
/// <para>
/// Credential and session headers travel only inside the transport request and are bound solely by the transport's
/// hashed header fingerprint; they never appear in a security request, audit record, log, or exception. Redirects are
/// never followed (the response limit declares zero redirects), so credentials cannot reach another origin.
/// </para>
/// <para>
/// Response bodies stream: the returned message's content is the bounded network body, owned by the message and
/// released with it. The body is bounded by <see cref="McpClientOptionsSnapshot.MaximumHttpResponseBytes"/> and by a
/// duration of <see cref="McpClientOptionsSnapshot.HttpStreamTimeout"/> for <c>GET</c> event streams or the endpoint
/// request timeout for <c>POST</c> and <c>DELETE</c>; overrun faults the stream rather than truncating silently, and
/// the SDK's own reconnection resumes event streams. Refusals and failures surface as
/// <see cref="HttpRequestException"/> with content-free messages, and caller cancellation as
/// <see cref="OperationCanceledException"/>. Instances are owned by the <see cref="HttpClient"/> that wraps them.
/// </para>
/// </remarks>
internal sealed class NetworkMcpHttpHandler: HttpMessageHandler
{
    private readonly INetworkNameResolver _resolver;
    private readonly INetworkTransport _transport;
    private readonly ISecurityAuthoritySelector _authorities;
    private readonly IIdentifierGenerator<SecurityRequestId> _securityRequestIds;
    private readonly IIdentifierGenerator<NetworkOperationId> _operationIds;
    private readonly ProtectedSemanticOperationContext _operation;
    private readonly McpEndpoint _endpoint;
    private readonly Uri _endpointUri;
    private readonly McpClientOptionsSnapshot _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;

    /// <summary>Initializes the handler for one HTTP endpoint and one captured protected operation.</summary>
    /// <param name="resolver">The protected destination resolver.</param>
    /// <param name="transport">The protected request transport.</param>
    /// <param name="authorities">Selects the authority the captured authorization names.</param>
    /// <param name="securityRequestIds">The replaceable security-request identity source.</param>
    /// <param name="operationIds">The replaceable network-operation identity source.</param>
    /// <param name="operation">The captured identity and authorization every exchange is authorized under.</param>
    /// <param name="endpoint">The configured endpoint; its transport profile must be HTTP.</param>
    /// <param name="options">The captured client bounds and classification.</param>
    /// <param name="timeProvider">The replaceable clock that owns exchange deadlines.</param>
    /// <param name="logger">The content-free logger; a null value selects a null logger.</param>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="endpoint"/> does not use the HTTP transport profile.</exception>
    internal NetworkMcpHttpHandler(
        INetworkNameResolver resolver,
        INetworkTransport transport,
        ISecurityAuthoritySelector authorities,
        IIdentifierGenerator<SecurityRequestId> securityRequestIds,
        IIdentifierGenerator<NetworkOperationId> operationIds,
        ProtectedSemanticOperationContext operation,
        McpEndpoint endpoint,
        McpClientOptionsSnapshot options,
        TimeProvider timeProvider,
        ILogger? logger)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(authorities);
        ArgumentNullException.ThrowIfNull(securityRequestIds);
        ArgumentNullException.ThrowIfNull(operationIds);
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        if (endpoint.Transport is not McpHttpTransportProfile http)
        {
            throw new ArgumentException("The endpoint must use the HTTP transport profile.", nameof(endpoint));
        }

        _resolver = resolver;
        _transport = transport;
        _authorities = authorities;
        _securityRequestIds = securityRequestIds;
        _operationIds = operationIds;
        _operation = operation;
        _endpoint = endpoint;
        _endpointUri = http.Endpoint;
        _options = options;
        _timeProvider = timeProvider;
        _logger = logger ?? NullLogger.Instance;
    }

    /// <inheritdoc/>
    /// <exception cref="HttpRequestException">
    /// The request targets another origin or an unsupported method, or authority, authorization, resolution,
    /// enforcement, audit, transport, redirect policy, or a bound refused or failed the exchange.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (!TryCreateDestination(request, out var destination))
        {
            throw Refused("destination", "The request does not target the configured MCP endpoint origin with a supported method.");
        }

        var bounds = _endpoint.Bounds;
        var method = new NetworkMethod(request.Method.Method);
        var body = await FreezeBodyAsync(request, bounds.MaximumMessageBytes, cancellationToken).ConfigureAwait(false);
        if (body.Refused)
        {
            throw Refused("request-body", "The request body exceeds the configured message bound.");
        }

        var timeout = method == NetworkMethod.Get ? _options.HttpStreamTimeout : bounds.RequestTimeout;
        var deadline = _timeProvider.GetUtcNow().Add(timeout);
        var networkBounds = new NetworkBounds(
            new NetworkResolutionBounds(bounds.HandshakeTimeout),
            new NetworkRequestBounds(bounds.HandshakeTimeout, bounds.MaximumMessageBytes),
            new NetworkResponseBounds(timeout, _options.MaximumHttpResponseBytes, maximumRedirects: 0));
        var headers = CreateHeaders(request);
        var classification = _options.HttpDataClassification;

        var authorization = _operation.Authorization;
        var authority = await SelectAuthorityAsync(authorization, cancellationToken).ConfigureAwait(false)
            ?? throw Refused("authority", "No security authority was available for the captured authorization context.");

        var operationId = _operationIds.Create();
        var resolutionGrant = await AuthorizeAsync(
            authority,
            _resolver.SecurityAudience,
            [NetworkSecurityBinding.ResolutionResource(destination)],
            NetworkSecurityBinding.ResolutionFingerprint(operationId, destination, networkBounds),
            deadline,
            cancellationToken).ConfigureAwait(false)
            ?? throw Refused("resolution", "Network resolution was not authorized.");
        var resolution = await _resolver.ResolveAsync(
            new NetworkResolutionRequest(operationId, destination, networkBounds, resolutionGrant),
            cancellationToken).ConfigureAwait(false);
        if (resolution is not NetworkResolved resolved)
        {
            throw resolution is NetworkResolutionDenied
                ? Refused("resolution", "Network resolution was denied.")
                : Failed("resolution", HttpRequestError.NameResolutionError, "The MCP endpoint could not be resolved.");
        }

        var sendGrant = await AuthorizeAsync(
            authority,
            _transport.SecurityAudience,
            NetworkSecurityBinding.RequestResources(destination, resolved.Addresses),
            NetworkSecurityBinding.RequestFingerprint(
                operationId,
                method,
                destination,
                headers,
                body.Content,
                networkBounds,
                resolved.Addresses,
                classification),
            deadline,
            cancellationToken).ConfigureAwait(false)
            ?? throw Refused("send", "Network egress was not authorized.");

        var send = await _transport.SendAsync(
            new NetworkRequest(
                operationId,
                method,
                destination,
                headers,
                body.Content,
                networkBounds,
                resolved.Addresses,
                classification,
                sendGrant),
            cancellationToken).ConfigureAwait(false);
        if (send is not NetworkResponseReceived received)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw MapSendFailure(send);
        }

        McpClientLog.HttpAnswered(_logger, _endpoint.Key, received.Response.Metadata.StatusCode);
        return CreateResponse(request, received.Response);
    }

    private HttpRequestException MapSendFailure(NetworkSendResult send) => send switch
    {
        NetworkDenied => Refused("send", "Network egress was denied."),
        NetworkRequestFailed { Kind: NetworkFailureKind.Timeout } =>
            Failed("send", HttpRequestError.ConnectionError, "The MCP endpoint did not answer before the deadline."),
        NetworkRequestFailed { Kind: NetworkFailureKind.TlsFailure } =>
            Failed("send", HttpRequestError.SecureConnectionError, "A secure connection to the MCP endpoint could not be established."),
        NetworkRequestFailed =>
            Failed("send", HttpRequestError.ConnectionError, "The MCP endpoint did not answer."),
        NetworkRedirectReceived or NetworkRedirectLimitExceeded =>
            Failed("send", HttpRequestError.InvalidResponse, "The MCP endpoint answered with a redirect, which is never followed."),
        NetworkResponseLimitExceeded =>
            Failed("send", HttpRequestError.ConfigurationLimitExceeded, "The MCP response exceeded the configured response bound."),
        _ => Failed("send", HttpRequestError.Unknown, "The network transport returned an unsupported result."),
    };

    private static HttpResponseMessage CreateResponse(HttpRequestMessage request, INetworkResponse response)
    {
        var message = new HttpResponseMessage((System.Net.HttpStatusCode) response.Metadata.StatusCode)
        {
            RequestMessage = request,
            Content = new StreamContent(new NetworkResponseStream(response)),
        };
        foreach (var header in response.Metadata.Headers.Headers)
        {
            if (!message.Headers.TryAddWithoutValidation(header.Name, header.Value))
            {
                _ = message.Content.Headers.TryAddWithoutValidation(header.Name, header.Value);
            }
        }

        return message;
    }

    private bool TryCreateDestination(HttpRequestMessage request, out NetworkDestination destination)
    {
        destination = null!;
        var uri = request.RequestUri;
        if (uri is not { IsAbsoluteUri: true }
            || (request.Method != HttpMethod.Get && request.Method != HttpMethod.Post && request.Method != HttpMethod.Delete)
            || !string.Equals(uri.Scheme, _endpointUri.Scheme, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(uri.IdnHost, _endpointUri.IdnHost, StringComparison.OrdinalIgnoreCase)
            || uri.Port != _endpointUri.Port
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Fragment))
        {
            return false;
        }

        var route = uri.GetComponents(UriComponents.PathAndQuery, UriFormat.UriEscaped);
        try
        {
            destination = new NetworkDestination(
                uri.Scheme,
                new NormalizedHost(uri.IdnHost),
                uri.Port,
                string.IsNullOrEmpty(route) ? NetworkRoute.Root : new NetworkRoute($"/{route.TrimStart('/')}"));
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static async ValueTask<FrozenBody> FreezeBodyAsync(
        HttpRequestMessage request,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        if (request.Content is not { } content)
        {
            return new FrozenBody(null, Refused: false);
        }

        if (content.Headers.ContentLength is { } declared && declared > maximumBytes)
        {
            return new FrozenBody(null, Refused: true);
        }

        var bytes = await content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        if (bytes.Length > maximumBytes)
        {
            return new FrozenBody(null, Refused: true);
        }

        var contentType = content.Headers.ContentType?.ToString() ?? "application/octet-stream";
        return new FrozenBody(new NetworkRequestContent(contentType, bytes), Refused: false);
    }

    private static NetworkHeaderSet CreateHeaders(HttpRequestMessage request)
    {
        var builder = ImmutableArray.CreateBuilder<NetworkHeader>();
        foreach (var header in request.Headers)
        {
            foreach (var value in header.Value)
            {
                builder.Add(new NetworkHeader(header.Key, value));
            }
        }

        return new NetworkHeaderSet(builder.ToImmutable());
    }

    private async ValueTask<ISecurityAuthority?> SelectAuthorityAsync(
        SecurityAuthorizationContext authorization,
        CancellationToken cancellationToken)
    {
        try
        {
            var selection = await _authorities.SelectAsync(authorization, cancellationToken).ConfigureAwait(false);
            return selection is SecurityAuthoritySelected selected && selected.Authorization == authorization
                ? selected.Authority
                : null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return null;
        }
    }

    private async ValueTask<SecurityGrant?> AuthorizeAsync(
        ISecurityAuthority authority,
        ComponentId audience,
        ImmutableArray<ProtectedResource> resources,
        InputFingerprint fingerprint,
        DateTimeOffset deadline,
        CancellationToken cancellationToken)
    {
        var authorization = _operation.Authorization;
        try
        {
            var decision = await authority.AuthorizeAsync(
                new SecurityRequest(
                    _securityRequestIds.Create(),
                    authorization.Scope,
                    toolCallId: null,
                    authorization.Identity,
                    authorization,
                    audience,
                    SecurityOperationKind.Network,
                    SecurityEffect.Egress,
                    resources,
                    fingerprint,
                    deadline),
                hooks: null,
                cancellationToken).ConfigureAwait(false);
            return decision is SecurityAllowed allowed ? allowed.Grant : null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return null;
        }
    }

    private HttpRequestException Refused(string stage, string safeMessage)
    {
        McpClientLog.HttpRefused(_logger, _endpoint.Key, stage);
        return new HttpRequestException(HttpRequestError.Unknown, safeMessage);
    }

    private HttpRequestException Failed(string stage, HttpRequestError error, string safeMessage)
    {
        McpClientLog.HttpFailed(_logger, _endpoint.Key, stage);
        return new HttpRequestException(error, safeMessage);
    }

    private readonly record struct FrozenBody(NetworkRequestContent? Content, bool Refused);
}
