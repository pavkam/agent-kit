// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports the profile selected for the requesting scope.</summary>
public sealed record HookProfileSelected: HookProfileSelectionResult
{
    /// <summary>Initializes a successful selection.</summary>
    /// <param name="profileKey">The selected profile.</param>
    /// <exception cref="ArgumentException"><paramref name="profileKey"/> is default (blank).</exception>
    public HookProfileSelected(HookProfileKey profileKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileKey.Value, nameof(profileKey));
        ProfileKey = profileKey;
    }

    /// <summary>Gets the selected profile.</summary>
    public HookProfileKey ProfileKey { get; }
}
