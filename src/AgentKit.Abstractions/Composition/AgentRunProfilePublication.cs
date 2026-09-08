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

    /// <summary>Gets the complete exact security-profile publication.</summary>
    public SecurityProfilePublication SecurityProfile { get; }

    /// <summary>Gets the compiled immutable session profile.</summary>
    public SessionProfileSnapshot SessionProfile { get; }
}
