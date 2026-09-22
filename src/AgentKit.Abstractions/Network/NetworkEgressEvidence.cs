// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports bytes actually sent and the secret-free egress fingerprint.</summary>
public sealed record NetworkEgressEvidence
{
    /// <summary>Initializes egress evidence for one completed send.</summary>
    /// <param name="sentBytes">The non-negative number of request body bytes transmitted.</param>
    /// <param name="egressFingerprint">The fingerprint of bytes actually sent.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="sentBytes"/> is negative.</exception>
    /// <exception cref="ArgumentException"><paramref name="egressFingerprint"/> is default.</exception>
    public NetworkEgressEvidence(long sentBytes, ContentHash egressFingerprint)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sentBytes);
        ArgumentOutOfRangeException.ThrowIfEqual(egressFingerprint, default);
        SentBytes = sentBytes;
        EgressFingerprint = egressFingerprint;
    }

    /// <summary>Gets the number of request body bytes transmitted.</summary>
    public long SentBytes { get; init; }

    /// <summary>Gets the fingerprint of bytes actually sent.</summary>
    public ContentHash EgressFingerprint { get; init; }
}
