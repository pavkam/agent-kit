// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Stable default keys for first-party <see cref="IContextAssembler"/> registration.</summary>
/// <remarks>
/// A definition selects its assembler through <see cref="AgentComponentSelection.Context"/>. Registering
/// <c>AddAgentContext(AgentContextComponentDefaults.AssemblerKey)</c> registers the key a definition names when it
/// selects the first-party default.
/// </remarks>
public static class AgentContextComponentDefaults
{
    /// <summary>The string value of <see cref="AssemblerKey"/>, exposed as a compile-time constant.</summary>
    public const string AssemblerKeyValue = "agentkit-default-context";

    /// <summary>Gets the canonical default assembler key.</summary>
    /// <value>A stable, nonblank key independent of any loop key.</value>
    public static ComponentKey<IContextAssembler> AssemblerKey { get; } = new(AssemblerKeyValue);
}
