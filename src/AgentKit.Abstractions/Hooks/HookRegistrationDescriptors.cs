// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Collections.Immutable;

/// <summary>Helpers for constructing <see cref="HookRegistrationDescriptor"/> values at registration time.</summary>
public static class HookRegistrationDescriptors
{
    /// <summary>The default profile key used when an application does not name a hook profile.</summary>
    public static HookProfileKey DefaultProfileKey { get; } = new("default");

    /// <summary>Creates one registration descriptor for a first-party agent loop point.</summary>
    /// <param name="authorId">The author-supplied hook identity used to derive the registration identity.</param>
    /// <param name="pointRegistration">The closed point metadata for the target point.</param>
    /// <param name="order">The requested coarse ordering anchor.</param>
    /// <param name="lifetime">The activation lifetime for the implementation.</param>
    /// <param name="requestedFailureMode">The failure mode this registration requests.</param>
    /// <param name="reentrancy">Whether this registration may be re-entered.</param>
    /// <param name="before">Soft ordering edges that must run after this registration when present.</param>
    /// <param name="after">Soft ordering edges that must run before this registration when present.</param>
    /// <param name="dependsOn">Hard ordering edges that must be present and run before this registration.</param>
    /// <returns>A descriptor ready for <c>Add*Hook&lt;T&gt;</c> registration.</returns>
    public static HookRegistrationDescriptor ForPoint(
        HookId authorId,
        HookPointDefinitionRegistration pointRegistration,
        HookOrder? order = null,
        HookLifetime lifetime = HookLifetime.Singleton,
        HookFailureMode? requestedFailureMode = null,
        HookReentrancyPolicy reentrancy = HookReentrancyPolicy.Forbidden,
        ImmutableArray<HookId> before = default,
        ImmutableArray<HookId> after = default,
        ImmutableArray<HookId> dependsOn = default)
    {
        ArgumentNullException.ThrowIfNull(pointRegistration);
        return new HookRegistrationDescriptor(
            HookRegistrationIds.FromAuthorHookId(authorId),
            pointRegistration.Point,
            DefaultProfileKey,
            order ?? HookOrder.Normal,
            lifetime,
            requestedFailureMode ?? pointRegistration.FailureInvariant,
            reentrancy,
            HookRegistrationIds.FromAuthorHookIds(before),
            HookRegistrationIds.FromAuthorHookIds(after),
            HookRegistrationIds.FromAuthorHookIds(dependsOn));
    }
}
