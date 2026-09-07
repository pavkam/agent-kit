// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network;

using Microsoft.Extensions.Options;

/// <summary>Sends one authorized request to an exact still-valid resolved address.</summary>
/// <remarks>Redirects are returned unfollowed so every hop receives fresh DNS and egress authority.</remarks>
public sealed partial class DefaultNetworkTransport: INetworkTransport, IDisposable
{
    private static readonly HttpRequestOptionsKey<IPAddress> _resolvedAddressKey = new("AgentKit.Network.ResolvedAddress");
    private static readonly HttpRequestOptionsKey<TimeSpan> _connectTimeoutKey = new("AgentKit.Network.ConnectTimeout");
    private readonly ISecurityGrantStore _grantStore;
    private readonly NetworkDestinationPolicy _policy;
    private readonly TimeProvider _timeProvider;
    private readonly HttpMessageInvoker _invoker;
    private readonly ILogger<DefaultNetworkTransport> _logger;

    /// <summary>Initializes the protected HTTP transport.</summary>
    /// <param name="grantStore">The authoritative grant store.</param>
    /// <param name="timeProvider">The deterministic deadline and freshness clock.</param>
    /// <param name="options">The validated structural destination policy.</param>
    /// <param name="logger">The optional content-free diagnostic logger.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    public DefaultNetworkTransport(
        ISecurityGrantStore grantStore,
        TimeProvider timeProvider,
        IOptions<AgentNetworkOptions> options,
        ILogger<DefaultNetworkTransport>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(grantStore);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        _logger = logger ?? NullLogger<DefaultNetworkTransport>.Instance;
        _grantStore = grantStore;
        _timeProvider = timeProvider;
        _policy = options.Value.DestinationPolicy;
        _invoker = new HttpMessageInvoker(
            new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                MaxResponseHeadersLength = options.Value.MaximumResponseHeaderKilobytes,
                ConnectCallback = ConnectAsync,
            },
            disposeHandler: true);
    }

    /// <inheritdoc/>
    public ComponentId SecurityAudience { get; } = new("agentkit.network.transport");

    /// <inheritdoc/>
    private async ValueTask<NetworkSendResult> SendCoreAsync(
        NetworkRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var consumption = await _grantStore.ValidateAndConsumeAsync(
            request.Grant,
            new SecurityEnforcementRequest(
                request.Grant.Scope,
                request.Grant.Identity,
                SecurityAudience,
                SecurityOperationKind.Network,
                SecurityEffect.Egress,
                NetworkSecurityBinding.RequestResources(request),
                NetworkSecurityBinding.RequestFingerprint(request),
                request.Grant.RevocationVersion),
            cancellationToken).ConfigureAwait(false);
        if (consumption.Status != GrantConsumptionStatus.Consumed)
        {
            return new NetworkDenied(consumption.SafeMessage);
        }

        if (!_policy.AllowsSchemeAndHost(request.Destination))
        {
            return new NetworkDenied("The destination is excluded by the configured network policy.");
        }

        var now = _timeProvider.GetUtcNow();
        var resolved = request.ResolvedAddresses.FirstOrDefault(address =>
            address.ExpiresAt > now && _policy.AllowsAddress(address.Address));
        if (resolved is null)
        {
            return new NetworkDenied("No still-valid resolved address is permitted by the configured network policy.");
        }

        using var message = BuildMessage(request, resolved.Address);
        CancellationTokenSource? timeout = new(request.Bounds.ResponseTimeout, _timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        HttpResponseMessage response;
        try
        {
            response = await _invoker.SendAsync(message, linked.Token).ConfigureAwait(false);
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
                    responseDeadline));
        }
        catch
        {
            response.Dispose();
            timeout?.Dispose();
            throw;
        }
    }

    private static HttpRequestMessage BuildMessage(NetworkRequest request, IPAddress address)
    {
        var message = new HttpRequestMessage(new HttpMethod(request.Method.Value), request.Destination.ToString());
        message.Options.Set(_resolvedAddressKey, address);
        message.Options.Set(_connectTimeoutKey, request.Bounds.ConnectTimeout);
        foreach (var header in request.Headers.Headers)
        {
            _ = message.Headers.TryAddWithoutValidation(header.Name, header.Value);
        }

        if (request.Content is { } content)
        {
            message.Content = new ByteArrayContent(content.Body.ToArray());
            message.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(content.ContentType);
        }

        return message;
    }

    private async ValueTask<Stream> ConnectAsync(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        if (!context.InitialRequestMessage.Options.TryGetValue(_resolvedAddressKey, out var address))
        {
            throw new NetworkConnectFailedException("No authorized resolved address was attached.");
        }

        if (!context.InitialRequestMessage.Options.TryGetValue(_connectTimeoutKey, out var connectTimeout))
        {
            throw new NetworkConnectFailedException("No authorized connection deadline was attached.");
        }

        var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        using var timeout = new CancellationTokenSource(connectTimeout, _timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            await socket.ConnectAsync(address, context.DnsEndPoint.Port, linked.Token).ConfigureAwait(false);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch (OperationCanceledException exception)
            when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            socket.Dispose();
            throw new NetworkConnectFailedException(
                "The pinned-address connection exceeded its deadline.",
                NetworkFailureKind.Timeout,
                exception);
        }
        catch (OperationCanceledException)
        {
            socket.Dispose();
            throw;
        }
        catch (SocketException exception)
        {
            socket.Dispose();
            throw new NetworkConnectFailedException(
                "The pinned-address connection failed.",
                NetworkFailureKind.ConnectionFailed,
                exception);
        }
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
    public void Dispose() => _invoker.Dispose();
}
