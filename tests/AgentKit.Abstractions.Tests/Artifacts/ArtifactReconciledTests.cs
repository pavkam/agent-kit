// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Artifacts;


/// <summary>Verifies <see cref="ArtifactReconciled"/> validation.</summary>
public sealed class ArtifactReconciledTests
{
    [Theory]
    [InlineData(ArtifactReconciliationDisposition.ReferenceCommitted)]
    [InlineData(ArtifactReconciliationDisposition.Collected)]
    [InlineData(ArtifactReconciliationDisposition.AlreadyCollected)]
    public void Constructor_WhenDispositionIsDefined_RetainsDisposition(ArtifactReconciliationDisposition disposition)
    {
        var reconciled = new ArtifactReconciled(disposition);
        reconciled.Disposition.ShouldBe(disposition);
        _ = reconciled.ShouldBeAssignableTo<ArtifactReconciliationResult>();
    }

    [Fact]
    public void Constructor_WhenDispositionIsUndefined_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ArtifactReconciled((ArtifactReconciliationDisposition) 999)).ParamName.ShouldBe("disposition");
}
