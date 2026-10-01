// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Artifacts;


/// <summary>Verifies <see cref="ArtifactReconciliationPending"/> validation.</summary>
public sealed class ArtifactReconciliationPendingTests
{
    [Theory]
    [InlineData(ArtifactReconciliationPendingReason.RetentionWindowOpen)]
    [InlineData(ArtifactReconciliationPendingReason.EvidenceUnavailable)]
    [InlineData(ArtifactReconciliationPendingReason.RetentionHold)]
    public void Constructor_WhenReasonIsDefined_RetainsReason(ArtifactReconciliationPendingReason reason)
    {
        var pending = new ArtifactReconciliationPending(reason);
        pending.Reason.ShouldBe(reason);
        _ = pending.ShouldBeAssignableTo<ArtifactReconciliationResult>();
    }

    [Fact]
    public void Constructor_WhenReasonIsUndefined_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ArtifactReconciliationPending((ArtifactReconciliationPendingReason) 999)).ParamName.ShouldBe("reason");
}
