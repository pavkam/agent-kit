// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network;

using Microsoft.Extensions.Options;

/// <summary>Connects to one resolved address and streams one bounded HTTP response.</summary>
/// <remarks>
/// See <see cref="NetworkDestinationPolicy"/> for the reduced-scope
/// rationale shared by every implementation of this contract. The
/// transport never performs DNS resolution itself; it connects only to an
/// address supplied by a prior <see cref="INetworkNameResolver"/>
/// resolution, and returns a redirect target without following it so the
/// caller must repeat resolution and policy enforcement for every hop.
/// </remarks>
public sealed class DefaultNetworkTransport: INetworkTransport, IDisposable
{
    private static readonly HttpRequestOptionsKey<IPAddress> _resolvedAddressKey = new("AgentKit.Network.ResolvedAddress");

    private readonly NetworkDestinationPolicy _policy;
    private readonly TimeProvider _timeProvider;
    private readonly HttpMessageInvoker _invoker;

    /// <summary>Initializes a new instance of the <see cref="DefaultNetworkTransport"/> class.</summary>
    /// <param name="timeProvider">The deterministic deadline clock.</param>
    /// <param name="options">The validated structural destination policy.</param>
    /// <exception cref="ArgumentNullException"><paramref name="timeProvider"/> or <paramref name="options"/> is null.</exception>
    public DefaultNetworkTransport(TimeProvider timeProvider, IOptions<AgentNetworkOptions> options)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);

        _timeProvider = timeProvider;
        _policy = options.Value.DestinationPolicy;
        _invoker = new HttpMessageInvoker(
            new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                ConnectCallback = ConnectAsync,
            },
            disposeHandler: true);
    }

    /// <inheritdoc/>
    public async ValueTask<NetworkSendResult> SendAsync(NetworkRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_policy.AllowsSchemeAndHost(request.Destination))
        {
            return new NetworkDenied($"Destination '{request.Destination}' is not permitted by the configured policy.");
        }

        var now = _timeProvider.GetUtcNow();
        var resolved = request.ResolvedAddresses.FirstOrDefault(candidate =>
            candidate.ExpiresAt > now && _policy.AllowsAddress(candidate.Address));
        if (resolved is null)
        {
            return new NetworkDenied("No still-valid resolved address is permitted by the configured policy.");
        }

        using var httpRequest = BuildHttpRequestMessage(request, resolved.Address);
        using var timeoutSource = new CancellationTokenSource(request.Bounds.ResponseTimeout, _timeProvider);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);

        HttpResponseMessage httpResponse;
        try
        {
            httpResponse = await _invoker.SendAsync(httpRequest, linkedSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new NetworkCancelled(sideEffectCertain: false);
        }
        catch (OperationCanceledException)
        {
            return new NetworkRequestFailed(
                NetworkFailureKind.Timeout,
                "The request did not complete before its deadline.",
                sideEffectCertain: false);
        }
        catch (HttpRequestException exception) when (exception.InnerException is NetworkConnectFailedException)
        {
            return new NetworkRequestFailed(
                NetworkFailureKind.ConnectionFailed,
                "The connection could not be established.",
                sideEffectCertain: true);
        }
        catch (HttpRequestException)
        {
            return new NetworkRequestFailed(
                NetworkFailureKind.ConnectionFailed,
                "The request failed.",
                sideEffectCertain: false);
        }

        if (IsRedirect(httpResponse.StatusCode) && httpResponse.Headers.Location is { } location)
        {
            var destination = ResolveRedirectDestination(request.Destination, location);
            var crossOrigin = !destination.Host.Equals(request.Destination.Host)
                || destination.Scheme != request.Destination.Scheme
                || destination.Port != request.Destination.Port;
            httpResponse.Dispose();
            return new NetworkRedirectReceived(destination, crossOrigin);
        }

        var headers = ToHeaderSet(httpResponse.Headers, httpResponse.Content.Headers);
        var declaredLength = httpResponse.Content.Headers.ContentLength;
        if (declaredLength is { } length && length > request.Bounds.MaximumResponseBytes)
        {
            httpResponse.Dispose();
            return new NetworkResponseLimitExceeded(length, request.Bounds.MaximumResponseBytes);
        }

        var metadata = new NetworkResponseMetadata((int) httpResponse.StatusCode, headers, declaredLength);
        var rawContent = await httpResponse.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return new NetworkResponseReceived(
            new RealNetworkResponse(
                httpResponse,
                new BoundedReadStream(rawContent, request.Bounds.MaximumResponseBytes),
                metadata));
    }

    private static HttpRequestMessage BuildHttpRequestMessage(NetworkRequest request, IPAddress resolvedAddress)
    {
        var httpRequest = new HttpRequestMessage(new HttpMethod(request.Method.Value), request.Destination.ToString());
        httpRequest.Options.Set(_resolvedAddressKey, resolvedAddress);
        foreach (var header in request.Headers.Headers)
        {
            _ = httpRequest.Headers.TryAddWithoutValidation(header.Name, header.Value);
        }

        if (request.Content is { } content)
        {
            httpRequest.Content = new ByteArrayContent(content.Body.ToArray());
            httpRequest.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(content.ContentType);
        }

        return httpRequest;
    }

    private static async ValueTask<Stream> ConnectAsync(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        if (!context.InitialRequestMessage.Options.TryGetValue(_resolvedAddressKey, out var address))
        {
            throw new NetworkConnectFailedException("No resolved address was attached to the request.");
        }

        var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(address, context.DnsEndPoint.Port, cancellationToken).ConfigureAwait(false);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch (Exception exception) when (exception is SocketException or OperationCanceledException)
        {
            socket.Dispose();
            throw new NetworkConnectFailedException("The connection could not be established.", exception);
        }
    }

    private static bool IsRedirect(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.MovedPermanently or HttpStatusCode.Found or HttpStatusCode.SeeOther
            or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect;

    private static NetworkDestination ResolveRedirectDestination(NetworkDestination current, Uri location)
    {
        var absolute = location.IsAbsoluteUri ? location : new Uri(new Uri(current.ToString()), location);
        var port = absolute.IsDefaultPort
            ? string.Equals(absolute.Scheme, "https", StringComparison.OrdinalIgnoreCase) ? 443 : 80
            : absolute.Port;
        var route = string.IsNullOrEmpty(absolute.PathAndQuery)
            ? NetworkRoute.Root
            : new NetworkRoute(absolute.PathAndQuery);
        return new NetworkDestination(absolute.Scheme, new NormalizedHost(absolute.Host), port, route);
    }

    private static NetworkHeaderSet ToHeaderSet(
        System.Net.Http.Headers.HttpHeaders responseHeaders,
        System.Net.Http.Headers.HttpHeaders contentHeaders)
    {
        var builder = ImmutableArray.CreateBuilder<NetworkHeader>();
        foreach (var header in responseHeaders.Concat(contentHeaders))
        {
            foreach (var value in header.Value)
            {
                builder.Add(new NetworkHeader(header.Key, value));
            }
        }

        return new NetworkHeaderSet(builder.ToImmutable());
    }

    /// <inheritdoc/>
    public void Dispose() => _invoker.Dispose();
}
