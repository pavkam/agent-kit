// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Declares how a scheduler treats a call whose <see cref="ToolSchedulingMode"/> is <see cref="ToolSchedulingMode.Unspecified"/>.</summary>
/// <remarks>
/// Untrusted tool hints never loosen host policy. A publisher that supplies no scheduling compatibility claim is
/// handled conservatively: the host decides once, in configuration, whether such a call runs as its own ordering
/// barrier or is refused outright rather than guessed as parallel-safe.
/// </remarks>
public enum UnknownSchedulingMode
{
    /// <summary>An unspecified call executes alone, forming an ordering barrier like <see cref="ToolSchedulingMode.Sequential"/>.</summary>
    Sequential,

    /// <summary>An unspecified call is rejected before any effect in the batch starts.</summary>
    Reject,
}
