// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Captures the exact security selection, policy snapshot, configuration, scope, and identity observed for an operation.</summary>
/// <remarks>This immutable context is evidence, not a grant. The security runtime resolves and revalidates its captured selections; effecting boundaries validate the resulting bounded grant, context binding, and live revocation immediately before acting.</remarks>
public sealed record SecurityAuthorizationContext
{
    /// <summary>Initializes captured authorization evidence.</summary>
    /// <param name="profileKey">The nondefault selected security profile.</param>
    /// <param name="profileVersion">The positive captured profile revision.</param>
    /// <param name="policySnapshot">The resolved immutable policy-snapshot reference.</param>
    /// <param name="authorityKey">The nondefault typed authority registration key.</param>
    /// <param name="agentDefinitionRevision">The captured nonnegative agent-definition revision.</param>
    /// <param name="configurationVersion">The positive captured effective-configuration revision.</param>
    /// <param name="scope">The exact agent, optional session, and causal operation scope.</param>
    /// <param name="identity">The complete immutable identity authenticated at trusted ingress.</param>
    /// <exception cref="ArgumentNullException"><paramref name="profileKey"/> or <paramref name="authorityKey"/> is default, or <paramref name="policySnapshot"/>, <paramref name="scope"/>, or <paramref name="identity"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="profileKey"/> or <paramref name="authorityKey"/> contains empty or whitespace text.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="profileVersion"/> or <paramref name="configurationVersion"/> is not positive, or <paramref name="agentDefinitionRevision"/> is negative.</exception>
    public SecurityAuthorizationContext(SecurityProfileKey profileKey, SecurityProfileVersion profileVersion, SecurityPolicySnapshotReference policySnapshot, ComponentKey<ISecurityAuthority> authorityKey, AgentDefinitionRevision agentDefinitionRevision, ConfigurationVersion configurationVersion, SecurityAuthorizationScope scope, ExecutionIdentity identity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileKey.Value, nameof(profileKey));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(profileVersion.Value, nameof(profileVersion));
        ArgumentNullException.ThrowIfNull(policySnapshot);
        ArgumentException.ThrowIfNullOrWhiteSpace(authorityKey.Value, nameof(authorityKey));
        ArgumentOutOfRangeException.ThrowIfNegative(agentDefinitionRevision.Value, nameof(agentDefinitionRevision));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(configurationVersion.Value, nameof(configurationVersion));
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(identity);
        ProfileKey = profileKey;
        ProfileVersion = profileVersion;
        PolicySnapshot = policySnapshot;
        AuthorityKey = authorityKey;
        AgentDefinitionRevision = agentDefinitionRevision;
        ConfigurationVersion = configurationVersion;
        Scope = scope;
        Identity = identity;
    }

    /// <summary>Gets the selected security profile key.</summary>
    /// <value>The exact explicitly selected profile identity.</value>
    public SecurityProfileKey ProfileKey { get; }
    /// <summary>Gets the captured security profile revision.</summary>
    /// <value>The positive revision observed during capture.</value>
    public SecurityProfileVersion ProfileVersion { get; }
    /// <summary>Gets the captured effective-policy snapshot.</summary>
    /// <value>The immutable snapshot identity, version, and fingerprint.</value>
    public SecurityPolicySnapshotReference PolicySnapshot { get; }
    /// <summary>Gets the typed authority registration key.</summary>
    /// <value>The exact authority selection captured for later live resolution.</value>
    public ComponentKey<ISecurityAuthority> AuthorityKey { get; }
    /// <summary>Gets the captured agent-definition revision.</summary>
    /// <value>The definition revision that selected the security profile.</value>
    public AgentDefinitionRevision AgentDefinitionRevision { get; }
    /// <summary>Gets the captured effective-configuration revision.</summary>
    /// <value>The positive effective configuration revision.</value>
    public ConfigurationVersion ConfigurationVersion { get; }
    /// <summary>Gets the exact authorization scope.</summary>
    /// <value>The immutable agent, session, and operation correlation.</value>
    public SecurityAuthorizationScope Scope { get; }
    /// <summary>Gets the complete authenticated execution identity.</summary>
    /// <value>The trusted-ingress identity snapshot; it grants no authority.</value>
    public ExecutionIdentity Identity { get; }
}
