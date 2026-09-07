// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Wraps a one-way, safe fingerprint of authentication evidence for correlation and cache partitioning.</summary>
public readonly record struct AuthenticationEvidenceFingerprint
{
    /// <summary>Initializes a safe evidence fingerprint.</summary>
    /// <param name="hash">The non-default content hash produced by trusted ingress; it must not encode a raw credential.</param>
    /// <exception cref="ArgumentException"><paramref name="hash"/> is default or blank.</exception>
    public AuthenticationEvidenceFingerprint(ContentHash hash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hash.Value, nameof(hash));
        Hash = hash;
    }

    /// <summary>Gets the one-way content hash.</summary>
    public ContentHash Hash { get; }

    /// <summary>Returns the safe fingerprint text.</summary>
    public override string ToString() => Hash.ToString();
}
