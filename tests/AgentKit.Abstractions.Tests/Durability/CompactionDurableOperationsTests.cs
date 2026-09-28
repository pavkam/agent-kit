// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies the compaction-activation boundary name and version are stable configuration values.</summary>
public sealed class CompactionDurableOperationsTests
{
    [Fact]
    public void Activation_WhenRead_IsTheStableBoundaryName() =>
        CompactionDurableOperations.Activation.Value.ShouldBe("agentkit.compaction.activation");

    [Fact]
    public void ActivationVersion_WhenRead_IsExplicitlyPublished() =>
        CompactionDurableOperations.ActivationVersion.Value.ShouldBe("v1");

    [Fact]
    public void All_WhenRead_ContainsEveryJournaledBoundaryExactlyOnce() =>
        CompactionDurableOperations.All.ShouldBe([CompactionDurableOperations.Activation]);
}
