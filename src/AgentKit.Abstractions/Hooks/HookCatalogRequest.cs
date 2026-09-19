// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Requests one immutable capture of every hook registration for one profile.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its fields, safe to share across threads
/// without synchronization. <see cref="IHookCatalog.CaptureAsync"/> and <see cref="IHookRegistrationSource.DiscoverAsync"/>
/// both take this request; it names only the profile being captured, never a specific hook point, because one
/// capture covers every point registered for that profile.
/// </remarks>
public sealed record HookCatalogRequest
{
    /// <summary>Initializes a new instance of the <see cref="HookCatalogRequest"/> record.</summary>
    /// <param name="profileKey">The hook profile to capture.</param>
    /// <exception cref="ArgumentException"><paramref name="profileKey"/> is default (blank).</exception>
    public HookCatalogRequest(HookProfileKey profileKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileKey.Value, nameof(profileKey));
        ProfileKey = profileKey;
    }

    /// <summary>Gets the hook profile to capture.</summary>
    public HookProfileKey ProfileKey { get; }
}
