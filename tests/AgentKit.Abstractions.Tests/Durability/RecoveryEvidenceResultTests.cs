// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies RecoveryEvidenceResult behavior and contracts.</summary>
public sealed class RecoveryEvidenceResultTests
{
    [Fact]
    public void RecoveryEvidenceResult_Hierarchy_ContainsOnlyTheThreeDeclaredKinds()
    {
        var kinds = typeof(RecoveryEvidenceResult).Assembly.GetTypes().Where(type => type.IsSubclassOf(typeof(RecoveryEvidenceResult))).Select(type => type.Name).OrderBy(name => name, StringComparer.Ordinal);
        kinds.ShouldBe([nameof(RecoveryEvidenceLoaded), nameof(RecoveryEvidenceNotFound), nameof(RecoveryEvidenceUnavailable),]);
    }
}
