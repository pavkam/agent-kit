// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Hooks;

/// <summary>Host configuration for one named hook profile.</summary>
/// <remarks>
/// Register profiles through <c>AddHookProfile</c> or <c>ReplaceHookProfile</c>. The built-in
/// <see cref="DefaultProfileKey"/> is always present after <c>AddAgentHooks</c>.
/// </remarks>
public sealed class HookProfileOptions
{
    /// <summary>Gets the canonical key for the engine-wide default hook profile.</summary>
    public static HookProfileKey DefaultProfileKey { get; } = new("default");

    /// <summary>
    /// Gets or sets an optional predicate that further filters registrations already bound to this profile key during
    /// catalog capture.
    /// </summary>
    /// <value>
    /// When null, every registration whose <see cref="HookRegistrationDescriptor.ProfileKey"/> matches this profile
    /// is included. When set, only registrations for which the predicate returns <see langword="true"/> are captured.
    /// </value>
    public Func<HookRegistrationDescriptor, bool>? RegistrationFilter { get; set; }

    /// <summary>
    /// Gets or sets the default failure mode this profile requests for every registration unless a narrower scope
    /// tightens the policy further.
    /// </summary>
    /// <value>
    /// When null, the profile contributes no additional failure baseline beyond the host and point invariants. When
    /// set, the dispatcher treats this value as a profile-wide tightening merged monotonically with each
    /// registration's own <see cref="HookRegistrationDescriptor.RequestedFailureMode"/>.
    /// </value>
    public HookFailureMode? DefaultRequestedFailureMode { get; set; }

    /// <summary>Gets or sets when reload for this profile may produce a newly captured catalog.</summary>
    /// <value>A defined <see cref="HookReloadBoundary"/>. The default is <see cref="HookReloadBoundary.NextRun"/>.</value>
    public HookReloadBoundary ReloadBoundary { get; set; } = HookReloadBoundary.NextRun;
}
