// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Binds one exact agent-definition revision to immutable security and session profile publications.</summary>
/// <remarks>This is host-supplied composition evidence. It contains no grant and does not authorize an effect.</remarks>
public sealed record AgentRunProfilePublication
{
    /// <summary>Initializes one exact immutable run-profile publication.</summary>
    /// <param name="securityProfile">The complete published security profile and configuration coordinates.</param>
    /// <param name="sessionProfile">The complete compiled session profile selected for the same definition.</param>
    /// <exception cref="ArgumentNullException">A parameter is null.</exception>
    public AgentRunProfilePublication(
        SecurityProfilePublication securityProfile,
        SessionProfileSnapshot sessionProfile)
    {
        ArgumentNullException.ThrowIfNull(securityProfile);
        ArgumentNullException.ThrowIfNull(sessionProfile);
        SecurityProfile = securityProfile;
        SessionProfile = sessionProfile;
    }

    /// <summary>Initializes one exact run-profile publication with its effective configuration snapshot.</summary>
    /// <param name="securityProfile">The complete published security profile and configuration coordinates.</param>
    /// <param name="sessionProfile">The complete compiled session profile selected for the same definition.</param>
    /// <param name="configuration">The exact effective configuration snapshot named by the security publication.</param>
    /// <exception cref="ArgumentNullException">A parameter is null.</exception>
    /// <exception cref="ArgumentException">The configuration version differs from the security publication, or its fingerprint differs from the session profile.</exception>
    public AgentRunProfilePublication(
        SecurityProfilePublication securityProfile,
        SessionProfileSnapshot sessionProfile,
        EffectiveConfigurationSnapshot configuration)
        : this(securityProfile, sessionProfile)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNotEqual(configuration.Version, securityProfile.ConfigurationVersion);
        ArgumentException.ThrowIfNotEqual(configuration.Fingerprint, sessionProfile.ConfigurationFingerprint);
        Configuration = configuration;
    }

    /// <summary>Gets the complete exact security-profile publication.</summary>
    public SecurityProfilePublication SecurityProfile { get; }

    /// <summary>Gets the compiled immutable session profile.</summary>
    public SessionProfileSnapshot SessionProfile { get; }

    /// <summary>Gets the exact effective configuration captured with this publication.</summary>
    /// <value>The exact snapshot, or null for legacy reduced publications that carry only its version.</value>
    public EffectiveConfigurationSnapshot? Configuration { get; }
}
