// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The ordering and identity metadata shared by every hook implementation,
/// independent of which specific hook point interface and
/// <see cref="AgentHookEventArgs"/> type it extends.
/// </summary>
/// <remarks>
/// <para>
/// This is deliberately the only member every hook point interface has in
/// common. AgentKit never exposes a generic hook that receives an arbitrary
/// event name and object payload; each named lifecycle boundary defines its
/// own dedicated interface (extending this one) with its own strongly typed
/// invocation method and dedicated <see cref="AgentHookEventArgs"/>-derived
/// argument type.
/// </para>
/// <para>
/// <see cref="Id"/> is chosen by whoever authors the hook and must be
/// unique among every hook registered for the same hook point; a
/// dispatcher rejects a catalog containing two registrations with the same
/// <see cref="Id"/>. <see cref="RunsBefore"/>, <see cref="RunsAfter"/>, and
/// <see cref="DependsOn"/> reference other hooks' <see cref="Id"/> values
/// and are only meaningful among hooks registered for the same point;
/// referencing a <see cref="HookId"/> that no registered hook declares at
/// that point is a missing-dependency composition failure.
/// </para>
/// </remarks>
public interface IHook
{
    /// <summary>Gets the stable identity of this hook registration.</summary>
    public HookId Id { get; }

    /// <summary>Gets the coarse ordering preference for this hook.</summary>
    public HookPriority Priority => HookPriority.Normal;

    /// <summary>
    /// Gets the identities of hooks, registered for the same point, that
    /// must run after this one.
    /// </summary>
    public ImmutableArray<HookId> RunsBefore => [];

    /// <summary>
    /// Gets the identities of hooks, registered for the same point, that
    /// must run before this one.
    /// </summary>
    public ImmutableArray<HookId> RunsAfter => [];

    /// <summary>
    /// Gets the identities of hooks, registered for the same point, that
    /// must also be present and must run before this one. Unlike
    /// <see cref="RunsAfter"/>, a missing dependency is a composition
    /// failure rather than a no-op ordering preference.
    /// </summary>
    public ImmutableArray<HookId> DependsOn => [];
}
