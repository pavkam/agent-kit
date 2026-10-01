// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;

/// <summary>
/// A deterministic <see cref="INetworkTransport"/> that consumes its send grant and answers through an
/// <see cref="HttpMessageHandler"/> script, so wire-translation tests keep their handler fixtures while the adapter
/// under test sends through the network boundary.
/// </summary>
/// <remarks>
/// The transport opens no socket. It reports cancellation, handler timeouts, and handler faults through the same typed
/// <see cref="NetworkSendResult"/> values the real transport uses, never as exceptions.
/// </remarks>
public sealed class HandlerNetworkTransport: INetworkTransport
{
    private readonly Lock _gate = new();
    private readonly HttpMessageHandler _handler;
    private readonly ISecurityGrantStore _grantStore;
    private readonly List<NetworkRequest> _requests = [];
    private int _intent;

    /// <summary>Initializes the transport over a handler script and grant store.</summary>
    /// <param name="handler">The handler that answers each request; it is borrowed, not disposed.</param>
    /// <param name="grantStore">The store that consumes send grants.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public HandlerNetworkTransport(HttpMessageHandler handler, ISecurityGrantStore grantStore)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(grantStore);
        _handler = handler;
        _grantStore = grantStore;
    }

    /// <inheritdoc/>
    public ComponentId SecurityAudience { get; } = new("test.network.transport");

    /// <summary>Gets a snapshot of every network request that reached the transport after grant consumption.</summary>
    public IReadOnlyList<NetworkRequest> Requests
    {
        get
        {
            lock (_gate)
            {
                return [.. _requests];
            }
        }
    }

    /// <inheritdoc/>
    public async ValueTask<NetworkSendResult> SendAsync(NetworkRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var enforcement = new SecurityEnforcementRequest(
            request.Grant.Scope,
            request.Grant.Identity,
            request.Grant.Authorization,
            SecurityAudience,
            SecurityOperationKind.Network,
            SecurityEffect.Egress,
            NetworkSecurityBinding.RequestResources(request),
            NetworkSecurityBinding.RequestFingerprint(request),
            request.Grant.RevocationVersion);
        var intent = new SecurityEnforcementIntent(
            new SecurityEnforcementIntentId(Guid.Parse($"80000000-0000-0000-0000-{Interlocked.Increment(ref _intent):D12}")),
            null);
        var consumption = await _grantStore
            .ValidateAndConsumeAsync(request.Grant, enforcement, intent, cancellationToken)
            .ConfigureAwait(false);
        if (consumption.Status != GrantConsumptionStatus.Consumed)
        {
            return new NetworkDenied(consumption.SafeMessage);
        }

        lock (_gate)
        {
            _requests.Add(request);
        }

        using var message = CreateMessage(request);
        using var invoker = new HttpMessageInvoker(_handler, disposeHandler: false);
        HttpResponseMessage response;
        try
        {
            response = await invoker.SendAsync(message, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new NetworkCancelled(sideEffectCertain: false);
        }
        catch (OperationCanceledException)
        {
            return new NetworkRequestFailed(NetworkFailureKind.Timeout, "The network request exceeded its deadline.", sideEffectCertain: false);
        }
        catch (HttpRequestException)
        {
            return new NetworkRequestFailed(NetworkFailureKind.ConnectionFailed, "The network request failed.", sideEffectCertain: false);
        }

        if (response.StatusCode is HttpStatusCode.MovedPermanently or HttpStatusCode.Found or HttpStatusCode.SeeOther
                or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect
            && response.Headers.Location is { } location)
        {
            response.Dispose();
            var port = location.IsDefaultPort ? location.Scheme == "https" ? 443 : 80 : location.Port;
            var destination = new NetworkDestination(
                location.Scheme,
                new NormalizedHost(location.Host),
                port,
                new NetworkRoute(location.PathAndQuery));
            return new NetworkRedirectReceived(
                destination,
                crossOrigin: destination.Authority != request.Destination.Authority);
        }

        var declared = response.Content.Headers.ContentLength;
        if (declared is { } length && length > request.Bounds.MaximumResponseBytes)
        {
            response.Dispose();
            return new NetworkResponseLimitExceeded(length, request.Bounds.MaximumResponseBytes);
        }

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return new NetworkResponseReceived(new HandlerNetworkResponse(response, stream, declared));
    }

    private static HttpRequestMessage CreateMessage(NetworkRequest request)
    {
        var message = new HttpRequestMessage(new HttpMethod(request.Method.Value), request.Destination.ToString());
        foreach (var header in request.Headers.Headers)
        {
            _ = message.Headers.TryAddWithoutValidation(header.Name, header.Value);
        }

        if (request.Content is NetworkRequestContent content)
        {
            var body = new ByteArrayContent(content.Body.ToArray());
            body.Headers.ContentType = MediaTypeHeaderValue.Parse(content.ContentType);
            message.Content = body;
        }

        return message;
    }

    private sealed class HandlerNetworkResponse: INetworkResponse
    {
        private readonly HttpResponseMessage _response;

        internal HandlerNetworkResponse(HttpResponseMessage response, Stream content, long? declared)
        {
            _response = response;
            Content = content;
            Metadata = new NetworkResponseMetadata(
                (int) response.StatusCode,
                new NetworkHeaderSet(
                [
                    .. response.Headers.Concat(response.Content.Headers)
                        .SelectMany(static header => header.Value.Select(value => new NetworkHeader(header.Key, value))),
                ]),
                declared);
        }

        public NetworkResponseMetadata Metadata { get; }

        public NetworkEgressEvidence? EgressEvidence => null;

        public Stream Content { get; }

        public ValueTask DisposeAsync()
        {
            _response.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
