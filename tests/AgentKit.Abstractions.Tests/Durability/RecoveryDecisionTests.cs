// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies RecoveryDecision behavior and contracts.</summary>
public sealed class RecoveryDecisionTests
{
    [Fact]
    public void RecoveryDecision_Hierarchy_ContainsOnlyTheSixDeclaredKinds()
    {
        var kinds = typeof(RecoveryDecision).Assembly.GetTypes().Where(type => type.IsSubclassOf(typeof(RecoveryDecision))).Select(type => type.Name).OrderBy(name => name, StringComparer.Ordinal);
        kinds.ShouldBe([nameof(RecoveryCommitRecordedResult), nameof(RecoveryNotPossible), nameof(RecoveryReconcileOperation), nameof(RecoveryRequiresOperator), nameof(RecoveryRetryOperation), nameof(RecoveryStartOperation),]);
    }
}
