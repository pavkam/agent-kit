// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Storage.Json;

/// <summary>Portable JSON mirror of <see cref="SecurityPolicySnapshotReference"/>, naming and fingerprinting one immutable effective policy snapshot.</summary>
/// <remarks>
/// <para>
/// The reference is captured evidence, not authority. Reconstructing it proves only that a snapshot identity, version, and
/// content fingerprint were recorded together; a consumer must still resolve and revalidate the reference against the live
/// policy catalog before relying on it, exactly as it would for an in-memory reference.
/// </para>
/// <para>
/// <see cref="SecurityPolicySnapshotId"/>, <see cref="SecurityPolicyVersion"/>, and <see cref="ContentHash"/> are unwrapped to
/// a <see cref="Guid"/>, an <see cref="long"/>, and text, and rebuilt through their validating constructors on read, so a
/// persisted empty identity, non-positive version, or blank fingerprint is rejected instead of being reconstructed as a
/// default-valued reference that would compare equal to another corrupted reference.
/// </para>
/// </remarks>
/// <param name="Id">The raw value of the non-empty policy-snapshot identity.</param>
/// <param name="Version">The positive published policy version captured with the snapshot.</param>
/// <param name="Fingerprint">The non-blank canonical content fingerprint of the effective policy set.</param>
public sealed record JsonSecurityPolicySnapshotReference(
    Guid Id,
    long Version,
    string Fingerprint)
{
    /// <summary>Projects one domain policy-snapshot reference into its portable JSON representation.</summary>
    /// <param name="value">The non-null reference to project.</param>
    /// <returns>A document carrying the unwrapped snapshot identity, version, and fingerprint text.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static JsonSecurityPolicySnapshotReference FromDomain(SecurityPolicySnapshotReference value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new JsonSecurityPolicySnapshotReference(value.Id.Value, value.Version.Value, value.Fingerprint.Value);
    }

    /// <summary>Reconstructs the exact domain reference this document was projected from.</summary>
    /// <returns>A reference equal to the projected original.</returns>
    /// <remarks>
    /// Each component is rebuilt through its own validating constructor before
    /// <see cref="SecurityPolicySnapshotReference(SecurityPolicySnapshotId, SecurityPolicyVersion, ContentHash)"/>
    /// revalidates the combination, so invalid persisted evidence fails closed on read.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The persisted snapshot identity is empty or <see cref="Version"/> is not positive.</exception>
    /// <exception cref="ArgumentException"><see cref="Fingerprint"/> is null, empty, or whitespace.</exception>
    public SecurityPolicySnapshotReference ToDomain()
    {
        return new SecurityPolicySnapshotReference(
            new SecurityPolicySnapshotId(Id),
            new SecurityPolicyVersion(Version),
            new ContentHash(Fingerprint));
    }
}
