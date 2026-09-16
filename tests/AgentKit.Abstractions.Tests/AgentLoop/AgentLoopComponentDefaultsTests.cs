// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.AgentLoop;

/// <summary>Verifies AgentLoopComponentDefaults behavior and contracts.</summary>
public sealed class AgentLoopComponentDefaultsTests
{
    [Fact]
    public void LoopKey_WhenRead_MatchesLoopKeyValue() =>
        AgentLoopComponentDefaults.LoopKey.ShouldBe(new ComponentKey<IAgentLoop>(AgentLoopComponentDefaults.LoopKeyValue));

    [Fact]
    public void ContinuationPolicyKey_WhenRead_MatchesContinuationPolicyKeyValue() =>
        AgentLoopComponentDefaults.ContinuationPolicyKey.ShouldBe(new ComponentKey<IRunContinuationPolicy>(AgentLoopComponentDefaults.ContinuationPolicyKeyValue));
}
