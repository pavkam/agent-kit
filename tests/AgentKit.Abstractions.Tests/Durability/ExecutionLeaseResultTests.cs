// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies ExecutionLeaseResult behavior and contracts.</summary>
public sealed class ExecutionLeaseResultTests
{
    [Fact]
    public void ExecutionLeaseResult_Hierarchy_ContainsOnlyTheTwoDeclaredKinds()
    {
        var kinds = typeof(ExecutionLeaseResult).Assembly.GetTypes().Where(type => type.IsSubclassOf(typeof(ExecutionLeaseResult))).Select(type => type.Name).OrderBy(name => name, StringComparer.Ordinal);
        kinds.ShouldBe([nameof(ExecutionLeaseAcquired), nameof(ExecutionLeaseHeldByAnotherWorker),]);
    }
}
