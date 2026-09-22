// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network;

using Microsoft.Extensions.Options;

/// <summary>Sends one authorized request to an exact still-valid resolved address.</summary>
/// <remarks>Redirects are returned unfollowed so every hop receives fresh DNS and egress authority.</remarks>
public sealed partial class DefaultNetworkTransport: INetworkTransport, IDisposable
{
    private readonly ISecurityGrantStore _grantStore;
    private readonly ISecurityAuditDispatcher _auditDispatcher;
    private readonly IIdentifierGenerator<SecurityAuditRecordId> _auditRecordIds;
    private readonly AgentNetworkOptionsSnapshot _options;
    private readonly TimeProvider _timeProvider;
    private readonly IIdentifierGenerator<SecurityEnforcementIntentId> _intentIds;
    private readonly NetworkTransportHandlerPool _handlerPool;
    private readonly ILogger<DefaultNetworkTransport> _logger;

    /// <summary>Initializes the protected HTTP transport.</summary>
    /// <param name="grantStore">The authoritative grant store.</param>
    /// <param name="auditDispatcher">The required security audit dispatcher.</param>
    /// <param name="timeProvider">The deterministic deadline and freshness clock.</param>
    /// <param name="options">The validated structural destination policy.</param>
    /// <param name="logger">The optional content-free diagnostic logger; a null value selects a null logger.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    public DefaultNetworkTransport(
        ISecurityGrantStore grantStore,
        ISecurityAuditDispatcher auditDispatcher,
        TimeProvider timeProvider,
        IOptions<AgentNetworkOptions> options,
        ILogger<DefaultNetworkTransport>? logger = null)
        : this(
            grantStore,
            auditDispatcher,
            timeProvider,
            options,
            logger,
            new GuidSecurityEnforcementIntentIdGenerator(),
            new GuidSecurityAuditRecordIdGenerator())
    {
    }

    /// <summary>Initializes the protected HTTP transport with injected identity sources.</summary>
    /// <param name="grantStore">The authoritative store that atomically consumes a grant and records permission to start.</param>
    /// <param name="auditDispatcher">The required security audit dispatcher.</param>
    /// <param name="timeProvider">The deterministic deadline and freshness clock.</param>
    /// <param name="options">The validated structural destination policy.</param>
    /// <param name="logger">The optional content-free diagnostic logger.</param>
    /// <param name="intentIds">The non-null thread-safe source of fresh per-send enforcement intent identities.</param>
    /// <param name="auditRecordIds">The non-null thread-safe source of audit record identities.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    public DefaultNetworkTransport(
        ISecurityGrantStore grantStore,
        ISecurityAuditDispatcher auditDispatcher,
        TimeProvider timeProvider,
        IOptions<AgentNetworkOptions> options,
        ILogger<DefaultNetworkTransport>? logger,
        IIdentifierGenerator<SecurityEnforcementIntentId> intentIds,
        IIdentifierGenerator<SecurityAuditRecordId> auditRecordIds)
        : this(
            CreateSnapshot(new NetworkProfileKey("default"), options),
            grantStore,
            auditDispatcher,
            auditRecordIds,
            timeProvider,
            intentIds,
            logger)
    {
    }

    internal DefaultNetworkTransport(
        AgentNetworkOptionsSnapshot options,
        ISecurityGrantStore grantStore,
        ISecurityAuditDispatcher auditDispatcher,
        IIdentifierGenerator<SecurityAuditRecordId> auditRecordIds,
        TimeProvider timeProvider,
        IIdentifierGenerator<SecurityEnforcementIntentId> intentIds,
        ILogger<DefaultNetworkTransport>? logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(grantStore);
        ArgumentNullException.ThrowIfNull(auditDispatcher);
        ArgumentNullException.ThrowIfNull(auditRecordIds);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(intentIds);
        _logger = logger ?? NullLogger<DefaultNetworkTransport>.Instance;
        _options = options;
        _grantStore = grantStore;
        _auditDispatcher = auditDispatcher;
        _auditRecordIds = auditRecordIds;
        _timeProvider = timeProvider;
        _intentIds = intentIds;
        _handlerPool = new NetworkTransportHandlerPool(options, timeProvider);
    }

    /// <inheritdoc/>
    public ComponentId SecurityAudience { get; } = new("agentkit.network.transport");

    /// <inheritdoc/>
    private async ValueTask<NetworkSendResult> SendCoreAsync(
        NetworkRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var enforcement = NetworkEnforcementReceipt.Create(
            request.Grant,
            SecurityAudience,
            NetworkSecurityBinding.RequestResources(request),
            NetworkSecurityBinding.RequestFingerprint(request));
        var intent = new SecurityEnforcementIntent(_intentIds.Create(), null);
        var denial = await NetworkHostGuard.ConsumeWithRequiredAuditAsync(
            request.Grant,
            enforcement,
            intent,
            _grantStore,
            _auditDispatcher,
            _auditRecordIds,
            _timeProvider,
            cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (denial is not null)
        {
            return new NetworkDenied(denial);
        }

        if (!_options.DestinationPolicy.AllowsSchemeAndHost(request.Destination))
        {
            return new NetworkDenied("The destination is excluded by the configured network policy.");
        }

        if (HasConnectionControllingHeader(request.Headers))
        {
            return new NetworkDenied(
                "The request headers include a connection-controlling header that would override the destination's virtual host, SNI/certificate target, or connection semantics.");
        }

        var now = _timeProvider.GetUtcNow();
        var resolved = request.ResolvedAddresses.FirstOrDefault(address =>
            address.ExpiresAt > now && _options.DestinationPolicy.AllowsAddress(address.Address));
        if (resolved is null)
        {
            return new NetworkDenied("No still-valid resolved address is permitted by the configured network policy.");
        }

        var upload = await NetworkUploadBuilder.BuildAsync(
            request.Content,
            request.Bounds.Request,
            cancellationToken).ConfigureAwait(false);
        if (upload is null)
        {
            return new NetworkDenied("The request body exceeds the authorized upload bounds or fingerprint.");
        }

        var partition = new NetworkConnectionPartitionKey(
            request.Destination,
            resolved.Address,
            _options.Proxy,
            _options.TlsPolicy,
            _options.DecompressionPolicy);
        var invoker = _handlerPool.GetInvoker(partition);
        using var message = BuildMessage(request, resolved.Address, upload.Content);
        CancellationTokenSource? timeout = new(request.Bounds.ResponseTimeout, _timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        HttpResponseMessage response;
        try
        {
            response = await invoker.SendAsync(message, linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            timeout.Dispose();
            return new NetworkCancelled(sideEffectCertain: false);
        }
        catch (OperationCanceledException)
        {
            timeout.Dispose();
            return new NetworkRequestFailed(
                NetworkFailureKind.Timeout,
                "The network request exceeded its deadline.",
                sideEffectCertain: false);
        }
        catch (HttpRequestException exception) when (exception.InnerException is NetworkConnectFailedException connectFailure)
        {
            timeout.Dispose();
            return new NetworkRequestFailed(
                connectFailure.Kind,
                connectFailure.Kind == NetworkFailureKind.Timeout
                    ? "The connection exceeded its deadline."
                    : "The connection could not be established.",
                sideEffectCertain: true);
        }
        catch (HttpRequestException)
        {
            timeout.Dispose();
            return new NetworkRequestFailed(
                NetworkFailureKind.ConnectionFailed,
                "The network request failed.",
                sideEffectCertain: false);
        }

        if (IsRedirect(response.StatusCode) && response.Headers.Location is { } location)
        {
            var destination = RedirectDestination(request.Destination, location);
            var crossOrigin = destination.Scheme != request.Destination.Scheme
                || !destination.Host.Equals(request.Destination.Host)
                || destination.Port != request.Destination.Port;
            response.Dispose();
            timeout.Dispose();
            return new NetworkRedirectReceived(destination, crossOrigin);
        }

        var declaredBytes = response.Content.Headers.ContentLength;
        if (declaredBytes is { } length && length > request.Bounds.MaximumResponseBytes)
        {
            response.Dispose();
            timeout.Dispose();
            return new NetworkResponseLimitExceeded(length, request.Bounds.MaximumResponseBytes);
        }

        var metadata = new NetworkResponseMetadata(
            (int) response.StatusCode,
            Headers(response.Headers, response.Content.Headers),
            declaredBytes);
        try
        {
            var stream = await response.Content.ReadAsStreamAsync(linked.Token).ConfigureAwait(false);
            var responseDeadline = timeout;
            timeout = null;
            return new NetworkResponseReceived(
                new RealNetworkResponse(
                    response,
                    new BoundedReadStream(stream, request.Bounds.MaximumResponseBytes, responseDeadline.Token),
                    metadata,
                    responseDeadline,
                    upload.Evidence));
        }
        catch
        {
            response.Dispose();
            timeout?.Dispose();
            throw;
        }
    }

    private static HttpRequestMessage BuildMessage(
        NetworkRequest request,
        IPAddress address,
        HttpContent? content)
    {
        var message = new HttpRequestMessage(new HttpMethod(request.Method.Value), request.Destination.ToString());
        message.Options.Set(NetworkTransportHandlerPool.ResolvedAddressKey, address);
        message.Options.Set(NetworkTransportHandlerPool.ConnectTimeoutKey, request.Bounds.ConnectTimeout);
        foreach (var header in request.Headers.Headers)
        {
            _ = message.Headers.TryAddWithoutValidation(header.Name, header.Value);
        }

        if (content is not null)
        {
            message.Content = content;
        }

        return message;
    }

    private static AgentNetworkOptionsSnapshot CreateSnapshot(
        NetworkProfileKey profileKey,
        IOptions<AgentNetworkOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var value = options.Value;
        return new AgentNetworkOptionsSnapshot(
            profileKey,
            new NetworkProfileVersion(1),
            value.DestinationPolicy,
            value.AddressResolutionLifetime,
            value.MaximumResponseHeaderKilobytes,
            value.Proxy,
            value.TlsPolicy,
            value.DecompressionPolicy);
    }

    /// <summary>
    /// Header names that, if forwarded verbatim, would let a caller-supplied value override the
    /// wire-level host/authority, TLS SNI/certificate target, or connection framing that
    /// <see cref="SocketsHttpHandler"/> derives from <see cref="HttpRequestMessage.Headers"/>.
    /// </summary>
    private static readonly string[] _connectionControllingHeaderNames = ["host", ":authority", "connection", "upgrade"];

    private static bool HasConnectionControllingHeader(NetworkHeaderSet headers)
    {
        foreach (var header in headers.Headers)
        {
            if (Array.Exists(
                    _connectionControllingHeaderNames,
                    name => string.Equals(name, header.Name, StringComparison.OrdinalIgnoreCase))
                || header.Name.StartsWith("Proxy-", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsRedirect(HttpStatusCode status) => status is
        HttpStatusCode.MovedPermanently or HttpStatusCode.Found or HttpStatusCode.SeeOther
        or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect;

    private static NetworkDestination RedirectDestination(NetworkDestination current, Uri location)
    {
        var absolute = location.IsAbsoluteUri ? location : new Uri(new Uri(current.ToString()), location);
        var port = absolute.IsDefaultPort
            ? string.Equals(absolute.Scheme, "https", StringComparison.OrdinalIgnoreCase) ? 443 : 80
            : absolute.Port;
        return new NetworkDestination(
            absolute.Scheme,
            new NormalizedHost(absolute.Host),
            port,
            string.IsNullOrEmpty(absolute.PathAndQuery) ? NetworkRoute.Root : new NetworkRoute(absolute.PathAndQuery));
    }

    private static NetworkHeaderSet Headers(
        System.Net.Http.Headers.HttpHeaders response,
        System.Net.Http.Headers.HttpHeaders content) => new(
        [.. response.Concat(content).SelectMany(static header =>
            header.Value.Select(value => new NetworkHeader(header.Key, value)))]);

    /// <inheritdoc/>
    public void Dispose() => _handlerPool.Dispose();
}
