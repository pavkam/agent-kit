// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Marks a hook event-argument type as supporting typed short-circuiting: a
/// hook may set an explicit, boundary-specific outcome that stops the
/// dispatcher from invoking any later hooks for that dispatch.
/// </summary>
/// <remarks>
/// This is the only short-circuiting mechanism AgentKit hooks support.
/// There is no general cancel flag: the boundary-specific outcome a hook
/// sets to short-circuit must itself be a typed value the owning operation
/// already knows how to interpret (allow, deny, replace-with-value, and so
/// on), never a bare boolean that silently means "stop and treat this as
/// success."
/// </remarks>
public interface IShortCircuitingHookArgs
{
    /// <summary>
    /// Gets a value indicating whether a hook has set an outcome that
    /// should stop the dispatcher from invoking any later hooks for this
    /// dispatch.
    /// </summary>
    public bool IsShortCircuited { get; }
}
