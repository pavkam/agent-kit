// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Publishes one immutable security-profile selection for exact agent and configuration coordinates.</summary>
/// <remarks>The publication is composition evidence. A reader must return it only for its exact agent, definition revision, configuration revision, and profile key; it supplies no current-policy fallback.</remarks>
public sealed record SecurityProfilePublication
{
    /// <summary>Initializes a validated security-profile publication.</summary>
    /// <param name="agentId">The non-default agent that owns the publication.</param>
    /// <param name="agentDefinitionRevision">The nonnegative definition revision that selected it.</param>
    /// <param name="configurationVersion">The positive configuration revision that published it.</param>
    /// <param name="profileKey">The nonblank selected profile key.</param>
    /// <param name="profileVersion">The positive immutable profile revision.</param>
    /// <param name="policySnapshot">The non-null selected policy snapshot.</param>
    /// <param name="authorityKey">The nonblank selected authority key.</param>
    /// <exception cref="ArgumentNullException"><paramref name="profileKey"/> or <paramref name="authorityKey"/> is default, or <paramref name="policySnapshot"/> is null.</exception>
    /// <exception cref="ArgumentException">A selected key is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An identity or revision is invalid.</exception>
    public SecurityProfilePublication(AgentId agentId, AgentDefinitionRevision agentDefinitionRevision,
        ConfigurationVersion configurationVersion, SecurityProfileKey profileKey, SecurityProfileVersion profileVersion,
        SecurityPolicySnapshotReference policySnapshot, ComponentKey<ISecurityAuthority> authorityKey)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, default, nameof(agentId));
        ArgumentOutOfRangeException.ThrowIfNegative(agentDefinitionRevision.Value, nameof(agentDefinitionRevision));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(configurationVersion.Value, nameof(configurationVersion));
        ArgumentException.ThrowIfNullOrWhiteSpace(profileKey.Value, nameof(profileKey));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(profileVersion.Value, nameof(profileVersion));
        ArgumentNullException.ThrowIfNull(policySnapshot);
        ArgumentException.ThrowIfNullOrWhiteSpace(authorityKey.Value, nameof(authorityKey));
        AgentId = agentId; AgentDefinitionRevision = agentDefinitionRevision; ConfigurationVersion = configurationVersion;
        ProfileKey = profileKey; ProfileVersion = profileVersion; PolicySnapshot = policySnapshot; AuthorityKey = authorityKey;
    }

    /// <summary>Gets the owning agent.</summary><value>The non-default agent for which this publication is valid.</value>
    public AgentId AgentId { get; }
    /// <summary>Gets the publishing definition revision.</summary><value>The nonnegative definition revision.</value>
    public AgentDefinitionRevision AgentDefinitionRevision { get; }
    /// <summary>Gets the publishing configuration revision.</summary><value>The positive configuration revision.</value>
    public ConfigurationVersion ConfigurationVersion { get; }
    /// <summary>Gets the explicit security profile key.</summary><value>The nonblank selected profile key.</value>
    public SecurityProfileKey ProfileKey { get; }
    /// <summary>Gets the immutable profile revision.</summary><value>The positive selected profile revision.</value>
    public SecurityProfileVersion ProfileVersion { get; }
    /// <summary>Gets the selected policy snapshot.</summary><value>The non-null immutable policy evidence.</value>
    public SecurityPolicySnapshotReference PolicySnapshot { get; }
    /// <summary>Gets the selected authority key.</summary><value>The nonblank key resolved later without fallback.</value>
    public ComponentKey<ISecurityAuthority> AuthorityKey { get; }
}
