// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Stable default keys for first-party <see cref="IContextAssembler"/> registration.</summary>
/// <remarks>
/// Until agent definitions expose an explicit context component key (WS18), run compilation resolves
/// <see cref="IContextAssembler"/> with the same string key as the selected loop. Registering
/// <c>AddAgentContext(AgentContextComponentDefaults.AssemblerKey)</c> therefore aligns with
/// <c>AddAgentLoop(AgentLoopComponentDefaults.LoopKey)</c> for otherwise-unconfigured agents.
/// </remarks>
public static class AgentContextComponentDefaults
{
    /// <summary>Gets the default assembler key shared with the default loop key.</summary>
    public static ComponentKey<IContextAssembler> AssemblerKey =>
        new(AgentLoopComponentDefaults.LoopKey.Value);
}
