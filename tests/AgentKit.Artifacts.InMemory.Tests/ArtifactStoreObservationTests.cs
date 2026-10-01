// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.InMemory.Tests;

/// <summary>Verifies bounded outcome labels and failure isolation of the shared store observation.</summary>
public sealed class ArtifactStoreObservationTests
{
    [Theory]
    [InlineData(ArtifactFailureKind.Denied, "denied")]
    [InlineData(ArtifactFailureKind.LimitExceeded, "limit_exceeded")]
    [InlineData(ArtifactFailureKind.IntegrityMismatch, "integrity_mismatch")]
    [InlineData(ArtifactFailureKind.NotFound, "not_found")]
    [InlineData(ArtifactFailureKind.Conflict, "conflict")]
    [InlineData(ArtifactFailureKind.RetentionConflict, "retention_conflict")]
    [InlineData(ArtifactFailureKind.Unavailable, "unavailable")]
    [InlineData(ArtifactFailureKind.Failed, "failed")]
    public void Name_WhenAFailureClassIsDefined_ReturnsAStableBoundedLabel(ArtifactFailureKind kind, string expected) =>
        ArtifactStoreObservation.Name(kind).ShouldBe(expected);

    [Fact]
    public void Name_WhenTheFailureClassIsUndefined_ThrowsNamingIt() =>
        Should.Throw<ArgumentOutOfRangeException>(() => ArtifactStoreObservation.Name((ArtifactFailureKind) 99)).ParamName.ShouldBe("kind");

    [Fact]
    public void Safe_WhenTheObservationThrows_SwallowsTheFailure() =>
        Should.NotThrow(() => ArtifactStoreObservation.Safe(static () => throw new InvalidOperationException("listener")));
}
