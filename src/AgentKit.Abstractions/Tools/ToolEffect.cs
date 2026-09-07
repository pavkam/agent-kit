// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The coarse effect category a tool declares on its <see cref="ToolDescriptor"/>,
/// used by authorization policy to distinguish tools that only observe state
/// from tools that change it.
/// </summary>
/// <remarks>
/// This is advisory metadata the tool author declares, not an enforced
/// sandbox boundary by itself; enforcement happens at the concrete
/// host-access implementation a mutating tool depends on (for example, a
/// sandboxed file system re-validating every write against its configured
/// root).
/// </remarks>
public enum ToolEffect
{
    /// <summary>The tool only observes state and never changes it.</summary>
    ReadOnly,

    /// <summary>The tool may change state outside the current operation.</summary>
    Mutating
}
