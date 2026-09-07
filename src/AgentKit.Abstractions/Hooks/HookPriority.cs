// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// A coarse ordering preference a hook may declare in addition to its
/// explicit <see cref="IHook.RunsBefore"/>, <see cref="IHook.RunsAfter"/>,
/// and <see cref="IHook.DependsOn"/> constraints.
/// </summary>
/// <remarks>
/// At most one registration per hook point should declare
/// <see cref="First"/>, and at most one should declare <see cref="Last"/>;
/// a dispatcher rejects a catalog with more than one of either as an
/// unsatisfiable ordering constraint, the same way it rejects a cycle.
/// </remarks>
public enum HookPriority
{
    /// <summary>No coarse preference; ordering is decided entirely by explicit constraints and registration order.</summary>
    Normal,

    /// <summary>This hook must run before every <see cref="Normal"/>-priority hook at the same point.</summary>
    First,

    /// <summary>This hook must run after every <see cref="Normal"/>-priority hook at the same point.</summary>
    Last
}
