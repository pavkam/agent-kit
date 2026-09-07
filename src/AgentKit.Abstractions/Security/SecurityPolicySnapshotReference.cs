// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Names and fingerprints one immutable effective security-policy snapshot.</summary>
/// <remarks>The reference is captured evidence only. A consumer must resolve and revalidate it against the live policy catalog before relying on it.</remarks>
public sealed record SecurityPolicySnapshotReference
{
    /// <summary>Initializes a policy-snapshot reference.</summary>
    /// <param name="id">The nondefault snapshot identity.</param>
    /// <param name="version">The positive published policy version.</param>
    /// <param name="fingerprint">The nonblank content fingerprint of the effective policy set.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is default or <paramref name="version"/> is not positive.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="fingerprint"/> is default.</exception>
    /// <exception cref="ArgumentException"><paramref name="fingerprint"/> contains empty or whitespace text.</exception>
    public SecurityPolicySnapshotReference(SecurityPolicySnapshotId id, SecurityPolicyVersion version, ContentHash fingerprint)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id.Value, Guid.Empty, nameof(id));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(version.Value, nameof(version));
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint.Value, nameof(fingerprint));
        Id = id;
        Version = version;
        Fingerprint = fingerprint;
    }

    /// <summary>Gets the snapshot identity.</summary>
    /// <value>The immutable snapshot identifier.</value>
    public SecurityPolicySnapshotId Id { get; }

    /// <summary>Gets the published policy version.</summary>
    /// <value>The positive version captured with the snapshot.</value>
    public SecurityPolicyVersion Version { get; }

    /// <summary>Gets the effective-policy content fingerprint.</summary>
    /// <value>The canonical fingerprint used to detect changed snapshot content.</value>
    public ContentHash Fingerprint { get; }
}
