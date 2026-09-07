// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Identity;

/// <summary>Configures trusted identity validation and delegation limits at composition time.</summary>
public sealed class AgentIdentityOptions
{
    /// <summary>Gets or sets whether a host may explicitly submit an anonymous identity. Defaults to <see langword="false"/>.</summary>
    public bool AllowAnonymous { get; set; }

    /// <summary>Gets or sets the maximum number of recorded delegation links. Defaults to eight.</summary>
    public int MaximumDelegationDepth { get; set; } = 8;

    /// <summary>Gets or sets the tolerated issuer-to-host clock difference. Defaults to two minutes.</summary>
    public TimeSpan MaximumClockSkew { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>Gets or sets the longest permitted authentication-evidence lifetime. Defaults to twelve hours.</summary>
    public TimeSpan MaximumEvidenceLifetime { get; set; } = TimeSpan.FromHours(12);
}
