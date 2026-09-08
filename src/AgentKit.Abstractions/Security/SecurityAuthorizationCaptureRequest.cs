// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Requests capture of an effective security profile and policy snapshot for one authenticated operation.</summary>
/// <remarks>The request contains no grant and does not authorize an effect. A profile selector resolves it only against an exact immutable publication, without fabricating missing configuration or identity.</remarks>
public sealed record SecurityAuthorizationCaptureRequest
{
    /// <summary>Initializes an authorization-capture request.</summary>
    /// <param name="scope">The exact agent, optional session, and causal operation scope.</param>
    /// <param name="profileKey">The explicitly selected nondefault security profile.</param>
    /// <param name="agentDefinitionRevision">The nonnegative agent-definition revision selecting the profile.</param>
    /// <param name="configurationVersion">The positive effective-configuration revision.</param>
    /// <param name="identity">The complete identity authenticated at trusted ingress.</param>
    /// <exception cref="ArgumentNullException"><paramref name="scope"/> or <paramref name="identity"/> is null, or <paramref name="profileKey"/> is default.</exception>
    /// <exception cref="ArgumentException"><paramref name="profileKey"/> contains empty or whitespace text.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="agentDefinitionRevision"/> is negative or <paramref name="configurationVersion"/> is not positive.</exception>
    public SecurityAuthorizationCaptureRequest(SecurityAuthorizationScope scope, SecurityProfileKey profileKey, AgentDefinitionRevision agentDefinitionRevision, ConfigurationVersion configurationVersion, ExecutionIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileKey.Value, nameof(profileKey));
        ArgumentOutOfRangeException.ThrowIfNegative(agentDefinitionRevision.Value, nameof(agentDefinitionRevision));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(configurationVersion.Value, nameof(configurationVersion));
        ArgumentNullException.ThrowIfNull(identity);
        Scope = scope;
        ProfileKey = profileKey;
        AgentDefinitionRevision = agentDefinitionRevision;
        ConfigurationVersion = configurationVersion;
        Identity = identity;
    }

    /// <summary>Gets the operation scope to bind.</summary>
    /// <value>The exact immutable operation scope.</value>
    public SecurityAuthorizationScope Scope { get; }
    /// <summary>Gets the explicitly selected security profile.</summary>
    /// <value>The exact profile identity supplied by composition.</value>
    public SecurityProfileKey ProfileKey { get; }
    /// <summary>Gets the captured agent-definition revision.</summary>
    /// <value>The definition revision selecting the profile.</value>
    public AgentDefinitionRevision AgentDefinitionRevision { get; }
    /// <summary>Gets the captured effective-configuration revision.</summary>
    /// <value>The positive effective configuration revision.</value>
    public ConfigurationVersion ConfigurationVersion { get; }
    /// <summary>Gets the complete authenticated execution identity.</summary>
    /// <value>The trusted-ingress identity to preserve during capture.</value>
    public ExecutionIdentity Identity { get; }
}
