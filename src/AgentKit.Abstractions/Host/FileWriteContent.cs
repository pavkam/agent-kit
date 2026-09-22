// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Bounded write payload bytes and their declared fingerprint.</summary>
/// <remarks>
/// The writer consumes at most the authorized bound and computes the payload
/// fingerprint over bytes actually read. Empty payloads are valid.
/// </remarks>
public sealed record FileWriteContent
{
    /// <summary>Initializes a new instance of the <see cref="FileWriteContent"/> record.</summary>
    /// <param name="payload">The payload bytes to write.</param>
    /// <param name="payloadFingerprint">The fingerprint of <paramref name="payload"/>.</param>
    public FileWriteContent(ReadOnlyMemory<byte> payload, ContentHash payloadFingerprint)
    {
        Payload = payload;
        PayloadFingerprint = payloadFingerprint;
    }

    /// <summary>Gets the payload bytes to write.</summary>
    public ReadOnlyMemory<byte> Payload { get; init; }

    /// <summary>Gets the fingerprint of <see cref="Payload"/>.</summary>
    public ContentHash PayloadFingerprint { get; init; }

    /// <inheritdoc/>
    public bool Equals(FileWriteContent? other) =>
        other is not null
        && PayloadFingerprint == other.PayloadFingerprint
        && Payload.Span.SequenceEqual(other.Payload.Span);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(PayloadFingerprint);
        hash.AddBytes(Payload.Span);
        return hash.ToHashCode();
    }
}
