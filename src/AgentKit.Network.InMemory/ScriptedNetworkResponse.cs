// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network.InMemory;

/// <summary>Owns a deterministic immutable response body.</summary>
public sealed class ScriptedNetworkResponse: INetworkResponse
{
    /// <summary>Initializes one scripted response.</summary>
    /// <param name="metadata">The immutable response metadata.</param>
    /// <param name="content">The exact body bytes copied into owned storage.</param>
    /// <param name="egressEvidence">Optional sent-byte and egress fingerprint evidence.</param>
    /// <exception cref="ArgumentNullException"><paramref name="metadata"/> is null.</exception>
    public ScriptedNetworkResponse(
        NetworkResponseMetadata metadata,
        ReadOnlyMemory<byte> content,
        NetworkEgressEvidence? egressEvidence = null)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        Metadata = metadata;
        EgressEvidence = egressEvidence;
        Content = new MemoryStream(content.ToArray(), writable: false);
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
        GC.SuppressFinalize(this);
    }
}
