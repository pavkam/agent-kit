// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that the requested hook profile is unknown to the composition.</summary>
public sealed record HookProfileUnavailable: HookProfileSelectionResult
{
    /// <summary>Initializes an unavailable selection.</summary>
    /// <param name="requestedProfile">The profile that could not be resolved.</param>
    /// <exception cref="ArgumentException"><paramref name="requestedProfile"/> is default (blank).</exception>
    public HookProfileUnavailable(HookProfileKey requestedProfile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestedProfile.Value, nameof(requestedProfile));
        RequestedProfile = requestedProfile;
    }

    /// <summary>Gets the profile that could not be resolved.</summary>
    public HookProfileKey RequestedProfile { get; }
}
