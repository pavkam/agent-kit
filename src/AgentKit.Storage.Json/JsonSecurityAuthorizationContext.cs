// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Storage.Json;

/// <summary>Portable JSON mirror of <see cref="SecurityAuthorizationContext"/>, the exact profile, policy snapshot, authority, configuration, scope, and identity captured for an operation.</summary>
/// <remarks>
/// <para>
/// This document is captured evidence, not a grant. Reconstructing it restores which selections were observed when the
/// operation was authorized; the security runtime still resolves and revalidates those selections, and an effecting boundary
/// still validates the resulting bounded grant and live revocation immediately before acting.
/// </para>
/// <para>
/// <see cref="AuthorityKey"/> unwraps the typed <see cref="ComponentKey{TContract}"/> selection for
/// <see cref="ISecurityAuthority"/> to its canonical key text. The type parameter is not persisted because it is fixed by this
/// mirror's contract, and reconstruction rebinds the text to the same closed contract, so a stored key can never be rebound to
/// a different service contract by a reader.
/// </para>
/// </remarks>
/// <param name="ProfileKey">The non-blank canonical key of the explicitly selected security profile.</param>
/// <param name="ProfileVersion">The positive captured security-profile revision.</param>
/// <param name="PolicySnapshot">The non-null resolved immutable policy-snapshot reference.</param>
/// <param name="AuthorityKey">The non-blank canonical key text of the typed <see cref="ISecurityAuthority"/> registration selection.</param>
/// <param name="AgentDefinitionRevision">The captured non-negative agent-definition revision that selected the security profile.</param>
/// <param name="ConfigurationVersion">The positive captured effective-configuration revision.</param>
/// <param name="Scope">The non-null exact agent, optional session, and causal operation scope.</param>
/// <param name="Identity">The non-null complete immutable identity authenticated at trusted ingress.</param>
public sealed record JsonSecurityAuthorizationContext(
    string ProfileKey,
    long ProfileVersion,
    JsonSecurityPolicySnapshotReference PolicySnapshot,
    string AuthorityKey,
    long AgentDefinitionRevision,
    long ConfigurationVersion,
    JsonSecurityAuthorizationScope Scope,
    JsonExecutionIdentity Identity)
{
    /// <summary>Projects one domain authorization context into its portable JSON representation.</summary>
    /// <param name="value">The non-null captured context to project.</param>
    /// <returns>A document carrying the unwrapped keys and revisions plus the projected snapshot, scope, and identity.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static JsonSecurityAuthorizationContext FromDomain(SecurityAuthorizationContext value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new JsonSecurityAuthorizationContext(
            value.ProfileKey.Value,
            value.ProfileVersion.Value,
            JsonSecurityPolicySnapshotReference.FromDomain(value.PolicySnapshot),
            value.AuthorityKey.Value,
            value.AgentDefinitionRevision.Value,
            value.ConfigurationVersion.Value,
            JsonSecurityAuthorizationScope.FromDomain(value.Scope),
            JsonExecutionIdentity.FromDomain(value.Identity));
    }

    /// <summary>Reconstructs the exact domain authorization context this document was projected from.</summary>
    /// <returns>A context equal to the projected original, including its nested snapshot, scope, and identity.</returns>
    /// <remarks>
    /// Each key, revision, and nested document is rebuilt through its own validating constructor before
    /// <see cref="SecurityAuthorizationContext"/> revalidates the combination, so persisted evidence that no longer satisfies
    /// the capture contract is rejected on read rather than replayed into an authorization decision.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><see cref="PolicySnapshot"/>, <see cref="Scope"/>, or <see cref="Identity"/> is null, which a well-formed document never is.</exception>
    /// <exception cref="ArgumentException"><see cref="ProfileKey"/> or <see cref="AuthorityKey"/> is null, empty, or whitespace, or a nested document carries invalid evidence.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="ProfileVersion"/> or <see cref="ConfigurationVersion"/> is not positive, <see cref="AgentDefinitionRevision"/> is negative, or a nested identity is empty.</exception>
    public SecurityAuthorizationContext ToDomain()
    {
        ArgumentNullException.ThrowIfNull(PolicySnapshot);
        ArgumentNullException.ThrowIfNull(Scope);
        ArgumentNullException.ThrowIfNull(Identity);
        return new SecurityAuthorizationContext(
            new SecurityProfileKey(ProfileKey),
            new SecurityProfileVersion(ProfileVersion),
            PolicySnapshot.ToDomain(),
            new ComponentKey<ISecurityAuthority>(AuthorityKey),
            new AgentDefinitionRevision(AgentDefinitionRevision),
            new ConfigurationVersion(ConfigurationVersion),
            Scope.ToDomain(),
            Identity.ToDomain());
    }
}
