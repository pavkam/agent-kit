// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Egress;

using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;

/// <summary>An owned provider response whose body streams from the network transport's bounded body stream.</summary>
/// <remarks>
/// <para>
/// The handle exposes the status, parsed response headers, and a content container whose
/// <see cref="HttpContent.ReadAsStreamAsync(CancellationToken)"/> returns the transport's own bounded stream without
/// buffering, so incremental parsing sees bytes at whatever fragmentation the transport delivers them. Actual
/// streamed overruns and body-deadline expiry surface from that stream as
/// <see cref="NetworkResponseTooLargeException"/> and <see cref="NetworkResponseTimedOutException"/>, never as a
/// truncated success.
/// </para>
/// <para>
/// The handle owns the underlying <see cref="INetworkResponse"/>. Dispose it exactly once with
/// <see cref="DisposeAsync"/>; disposal releases the connection according to framing state. No synchronous disposal
/// exists, because the network response disposes asynchronously.
/// </para>
/// </remarks>
public sealed class ProviderEgressResponse: IAsyncDisposable
{
    private readonly INetworkResponse _network;
    private readonly HttpResponseMessage _message;
    private int _disposed;

    /// <summary>Wraps one network response, taking ownership of its body stream and connection.</summary>
    /// <param name="network">The response whose status, headers, and bounded body stream this handle exposes.</param>
    /// <exception cref="ArgumentNullException"><paramref name="network"/> is null.</exception>
    /// <remarks>
    /// <see cref="ProviderEgress"/> is the production source of these handles; the constructor is public so adapters'
    /// response-handling logic can be verified against any <see cref="INetworkResponse"/>.
    /// </remarks>
    public ProviderEgressResponse(INetworkResponse network)
    {
        ArgumentNullException.ThrowIfNull(network);

        _network = network;
        var content = new StreamContent(network.Content);
        _message = new HttpResponseMessage((HttpStatusCode) network.Metadata.StatusCode) { Content = content };
        foreach (var header in network.Metadata.Headers.Headers)
        {
            if (!_message.Headers.TryAddWithoutValidation(header.Name, header.Value))
            {
                _ = content.Headers.TryAddWithoutValidation(header.Name, header.Value);
            }
        }
    }

    /// <summary>Gets the HTTP status code the provider returned.</summary>
    public HttpStatusCode StatusCode => _message.StatusCode;

    /// <summary>Gets a value indicating whether the status code is in the 2xx range.</summary>
    public bool IsSuccessStatusCode => _message.IsSuccessStatusCode;

    /// <summary>Gets the parsed non-content response headers.</summary>
    public HttpResponseHeaders Headers => _message.Headers;

    /// <summary>Gets the response content container whose stream is the transport's bounded body stream.</summary>
    public HttpContent Content => _message.Content;

    /// <summary>Releases the response body and the underlying network response.</summary>
    /// <returns>A task that completes when the network response has been released.</returns>
    /// <remarks>Repeated calls complete immediately after the first.</remarks>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        try
        {
            await _network.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            _message.Dispose();
        }
    }
}
