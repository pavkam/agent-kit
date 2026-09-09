// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Retains one complete security-profile publication as typed effective configuration.</summary>
/// <remarks>The publication is immutable historical selection evidence and does not grant or reactivate authority.</remarks>
public sealed record SecurityProfileConfigurationValue: ConfigurationSemanticValue
{
    /// <summary>Creates a typed security selection without converting from untrusted document text.</summary>
    /// <param name="publication">The complete nonnull profile publication.</param>
    /// <exception cref="ArgumentNullException"><paramref name="publication"/> is null.</exception>
    public SecurityProfileConfigurationValue(SecurityProfilePublication publication)
    {
        ArgumentNullException.ThrowIfNull(publication);
        Publication = publication;
    }

    /// <summary>Gets the complete selected security publication.</summary>
    /// <value>Exact agent, definition, configuration, profile, policy, and authority-key evidence.</value>
    public SecurityProfilePublication Publication { get; }
}
