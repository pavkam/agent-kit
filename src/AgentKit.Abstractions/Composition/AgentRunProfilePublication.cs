// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Binds one exact agent-definition revision to immutable security, session, hook, and budget profile selections.</summary>
/// <remarks>
/// This is host-supplied composition evidence. It contains no grant and does not authorize an effect. The hook and
/// budget profile references are the exact keys the definition selected; composition validation requires them to equal
/// the definition's own, so a pinned run cannot silently pair a revision with another revision's profiles.
/// </remarks>
public sealed record AgentRunProfilePublication
{
    /// <summary>Initializes one exact run-profile publication with its effective configuration snapshot.</summary>
    /// <param name="securityProfile">The complete published security profile and configuration coordinates.</param>
    /// <param name="sessionProfile">The complete compiled session profile selected for the same definition.</param>
    /// <param name="hookProfile">The nonblank hook profile key the definition selected.</param>
    /// <param name="budgetProfile">The nonblank budget profile key the definition selected.</param>
    /// <param name="configuration">The exact effective configuration snapshot named by the security publication.</param>
    /// <exception cref="ArgumentNullException">A reference parameter is null.</exception>
    /// <exception cref="ArgumentException">A profile key is the default value, the configuration version differs from the security publication, or its fingerprint differs from the session profile.</exception>
    public AgentRunProfilePublication(
        SecurityProfilePublication securityProfile,
        SessionProfileSnapshot sessionProfile,
        HookProfileKey hookProfile,
        BudgetProfileKey budgetProfile,
        EffectiveConfigurationSnapshot configuration)
    {
        ArgumentNullException.ThrowIfNull(securityProfile);
        ArgumentNullException.ThrowIfNull(sessionProfile);
        ArgumentException.ThrowIfNullOrWhiteSpace(hookProfile.Value, nameof(hookProfile));
        ArgumentException.ThrowIfNullOrWhiteSpace(budgetProfile.Value, nameof(budgetProfile));
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNotEqual(configuration.Version, securityProfile.ConfigurationVersion);
        ArgumentException.ThrowIfNotEqual(configuration.Fingerprint, sessionProfile.ConfigurationFingerprint);
        SecurityProfile = securityProfile;
        SessionProfile = sessionProfile;
        HookProfile = hookProfile;
        BudgetProfile = budgetProfile;
        Configuration = configuration;
    }

    /// <summary>Gets the complete exact security-profile publication.</summary>
    public SecurityProfilePublication SecurityProfile { get; }

    /// <summary>Gets the compiled immutable session profile.</summary>
    public SessionProfileSnapshot SessionProfile { get; }

    /// <summary>Gets the hook profile key the definition selected.</summary>
    /// <value>A nonblank key.</value>
    public HookProfileKey HookProfile { get; }

    /// <summary>Gets the budget profile key the definition selected.</summary>
    /// <value>A nonblank key.</value>
    public BudgetProfileKey BudgetProfile { get; }

    /// <summary>Gets the exact effective configuration captured with this publication.</summary>
    /// <value>The exact snapshot whose version and fingerprint match the security and session profiles.</value>
    public EffectiveConfigurationSnapshot Configuration { get; }
}
