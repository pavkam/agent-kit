// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Artifacts;


/// <summary>Verifies <see cref="ArtifactReconciliationRejected"/> validation.</summary>
public sealed class ArtifactReconciliationRejectedTests
{
    [Fact]
    public void Constructor_WhenCalledWithFailure_RetainsFailure()
    {
        var failure = new ArtifactFailure(ArtifactFailureKind.NotFound, "missing");
        var rejected = new ArtifactReconciliationRejected(failure);
        rejected.Failure.ShouldBe(failure);
        _ = rejected.ShouldBeAssignableTo<ArtifactReconciliationResult>();
    }

    [Fact]
    public void Constructor_WhenFailureIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new ArtifactReconciliationRejected(null!)).ParamName.ShouldBe("failure");
}
