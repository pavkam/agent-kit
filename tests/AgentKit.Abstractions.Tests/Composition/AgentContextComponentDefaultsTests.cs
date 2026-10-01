// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Composition;

/// <summary>Verifies AgentContextComponentDefaults behavior and contracts.</summary>
public sealed class AgentContextComponentDefaultsTests
{
    [Fact]
    public void AssemblerKey_WhenRead_MatchesAssemblerKeyValue() =>
        AgentContextComponentDefaults.AssemblerKey.ShouldBe(new ComponentKey<IContextAssembler>(AgentContextComponentDefaults.AssemblerKeyValue));

    [Fact]
    public void AssemblerKey_WhenRead_IsIndependentOfTheLoopKey() =>
        AgentContextComponentDefaults.AssemblerKey.Value.ShouldNotBe(AgentLoopComponentDefaults.LoopKey.Value);
}
