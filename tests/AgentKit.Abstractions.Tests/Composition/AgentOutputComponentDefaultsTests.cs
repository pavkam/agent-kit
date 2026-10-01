// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Composition;

/// <summary>Verifies AgentOutputComponentDefaults behavior and contracts.</summary>
public sealed class AgentOutputComponentDefaultsTests
{
    [Fact]
    public void ProcessorKey_WhenRead_MatchesProcessorKeyValue() =>
        AgentOutputComponentDefaults.ProcessorKey.ShouldBe(new ComponentKey<IOutputProcessor>(AgentOutputComponentDefaults.ProcessorKeyValue));

    [Fact]
    public void ProcessorKeyValue_WhenRead_IsTheStableDefaultOutputKey() =>
        AgentOutputComponentDefaults.ProcessorKeyValue.ShouldBe("agentkit-default-output");
}
