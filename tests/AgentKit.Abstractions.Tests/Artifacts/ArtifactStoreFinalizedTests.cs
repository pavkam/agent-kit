// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Artifacts;

using static AgentKit.Abstractions.Tests.Artifacts.ArtifactContractTestData;

/// <summary>Verifies <see cref="ArtifactStoreFinalized"/> validation.</summary>
public sealed class ArtifactStoreFinalizedTests
{
    [Fact]
    public void Constructor_WhenCalledWithValidArguments_RetainsReference()
    {
        var reference = Reference();
        var finalized = new ArtifactStoreFinalized(reference);
        finalized.Reference.ShouldBe(reference);
        _ = finalized.ShouldBeAssignableTo<ArtifactStoreFinalizeResult>();
    }

    [Fact]
    public void Constructor_WhenReferenceIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new ArtifactStoreFinalized(null!)).ParamName.ShouldBe("reference");
}
