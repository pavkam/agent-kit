// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Stable default keys for first-party <see cref="ICompactor"/> registration.</summary>
/// <remarks>
/// Until agent definitions expose an explicit compaction component key, run compilation resolves
/// <see cref="ICompactor"/> through the unkeyed registration registered by
/// <c>AddContextCompaction</c> or the keyed registration registered under this key via
/// <c>AddAgentContextCompaction</c>.
/// </remarks>
public static class AgentContextCompactionComponentDefaults
{
    /// <summary>Gets the default compactor key aligned with the default loop key.</summary>
    public static ComponentKey<ICompactor> CompactorKey =>
        new(AgentLoopComponentDefaults.LoopKey.Value);
}
