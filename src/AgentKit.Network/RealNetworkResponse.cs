// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network;

/// <summary>Owns one real HTTP response and its bounded body stream.</summary>
internal sealed class RealNetworkResponse: INetworkResponse
{
    private readonly HttpResponseMessage _response;
    private readonly CancellationTokenSource _responseDeadline;

    /// <summary>Initializes an owned response, body stream, and live response deadline.</summary>
    /// <param name="response">The underlying response that owns connection resources.</param>
    /// <param name="content">The already bounded body stream.</param>
    /// <param name="metadata">The immutable response metadata.</param>
    /// <param name="responseDeadline">The live transport-owned response deadline.</param>
    /// <param name="egressEvidence">Evidence of bytes sent for the paired request.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    internal RealNetworkResponse(
        HttpResponseMessage response,
        Stream content,
        NetworkResponseMetadata metadata,
        CancellationTokenSource responseDeadline,
        NetworkEgressEvidence? egressEvidence)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(responseDeadline);
        _response = response;
        _responseDeadline = responseDeadline;
        Content = content;
        Metadata = metadata;
        EgressEvidence = egressEvidence;
    }

    /// <inheritdoc/>
    public NetworkResponseMetadata Metadata { get; }

    /// <inheritdoc/>
    public NetworkEgressEvidence? EgressEvidence { get; }

    /// <inheritdoc/>
    public Stream Content { get; }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await Content.DisposeAsync().ConfigureAwait(false);
        _response.Dispose();
        _responseDeadline.Dispose();
        GC.SuppressFinalize(this);
    }
}
