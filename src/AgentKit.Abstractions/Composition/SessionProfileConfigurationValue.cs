// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Retains one exact session-profile reference as typed effective configuration.</summary>
public sealed record SessionProfileConfigurationValue: ConfigurationSemanticValue
{
    /// <summary>Creates a typed session selection without resolving a store or profile.</summary>
    /// <param name="profile">The nonnull exact profile key/version reference.</param>
    /// <exception cref="ArgumentNullException"><paramref name="profile"/> is null.</exception>
    public SessionProfileConfigurationValue(SessionProfileReference profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        Profile = profile;
    }

    /// <summary>Gets the exact selected session profile.</summary>
    /// <value>A nonnull immutable key/version reference with no live lookup.</value>
    public SessionProfileReference Profile { get; }
}
