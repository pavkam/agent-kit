// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Carries the profile and instant a memory policy evaluates a proposal under.</summary>
/// <remarks>The context exposes configuration facts only. It grants no authority, and a policy cannot widen the profile's classification ceiling through it.</remarks>
public sealed record MemoryPolicyContext
{
    /// <summary>Initializes a validated context.</summary>
    /// <param name="profile">The immutable profile the proposal runs under.</param>
    /// <param name="now">The evaluation instant, taken from the injected clock.</param>
    /// <exception cref="ArgumentNullException"><paramref name="profile"/> is null.</exception>
    public MemoryPolicyContext(MemoryProfileSnapshot profile, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(profile);
        Profile = profile;
        Now = now;
    }

    /// <summary>Gets the immutable profile the proposal runs under.</summary>
    public MemoryProfileSnapshot Profile { get; }

    /// <summary>Gets the evaluation instant.</summary>
    public DateTimeOffset Now { get; }
}
