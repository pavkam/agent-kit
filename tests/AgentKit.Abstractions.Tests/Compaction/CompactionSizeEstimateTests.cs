// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Compaction;

using AgentKit;

/// <summary>Verifies CompactionSizeEstimate behavior and contracts.</summary>
public sealed class CompactionSizeEstimateTests
{
    [Fact]
    public void CompactionSizeEstimate_Equality_WhenSameValues_InstancesAreEqual() => new CompactionSizeEstimate(1, 2, 3).ShouldBe(new CompactionSizeEstimate(1, 2, 3));
}
