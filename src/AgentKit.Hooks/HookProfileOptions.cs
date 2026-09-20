// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Hooks;

/// <summary>Host configuration for the default hook profile and future named profiles.</summary>
/// <remarks>
/// WS2-C10 extends this type with registration filters, per-profile failure defaults, and reload boundaries. Until
/// then, <see cref="DefaultHookProfileSelector"/> resolves every request to <see cref="DefaultProfileKey"/> when no
/// explicit profile is named, or validates that the request names that same profile.
/// </remarks>
public sealed class HookProfileOptions
{
    /// <summary>Gets the canonical key for the engine-wide default hook profile.</summary>
    public static HookProfileKey DefaultProfileKey { get; } = new("default");
}
