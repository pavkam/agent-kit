// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Artifacts;

using static AgentKit.Abstractions.Tests.Artifacts.ArtifactContractTestData;

/// <summary>Verifies <see cref="ArtifactReferenceCommitIntentResult"/> invariants.</summary>
public sealed class ArtifactReferenceCommitIntentResultTests
{
    private static readonly ArtifactReferenceCommitIntentId _id = new(Guid.Parse("a0000000-0000-0000-0000-0000000000a1"));

    [Theory]
    [InlineData(ArtifactReferenceCommitIntentOutcome.Applied)]
    [InlineData(ArtifactReferenceCommitIntentOutcome.Replayed)]
    [InlineData(ArtifactReferenceCommitIntentOutcome.Conflict)]
    [InlineData(ArtifactReferenceCommitIntentOutcome.StateChanged)]
    public void Constructor_WhenOutcomeHasIntent_RetainsBoth(ArtifactReferenceCommitIntentOutcome outcome)
    {
        var intent = Intent();
        var result = new ArtifactReferenceCommitIntentResult(outcome, intent);
        result.Outcome.ShouldBe(outcome);
        result.Intent.ShouldBe(intent);
    }

    [Fact]
    public void Constructor_WhenNotFoundHasNoIntent_RetainsOutcome()
    {
        var result = new ArtifactReferenceCommitIntentResult(ArtifactReferenceCommitIntentOutcome.NotFound, null);
        result.Intent.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WhenNotFoundCarriesIntent_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new ArtifactReferenceCommitIntentResult(ArtifactReferenceCommitIntentOutcome.NotFound, Intent())).ParamName.ShouldBe("intent");

    [Fact]
    public void Constructor_WhenAppliedHasNoIntent_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new ArtifactReferenceCommitIntentResult(ArtifactReferenceCommitIntentOutcome.Applied, null)).ParamName.ShouldBe("intent");

    [Fact]
    public void Constructor_WhenOutcomeIsUndefined_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ArtifactReferenceCommitIntentResult((ArtifactReferenceCommitIntentOutcome) 999, Intent())).ParamName.ShouldBe("outcome");

    private static ArtifactReferenceCommitIntent Intent() => new(
        _id, Identity.TenantId, PreparationId, ArtifactId, new ArtifactVersion("1"), new ArtifactOwnerId("session:owner"),
        new ArtifactPin(_id, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddHours(1)),
        ArtifactReferenceCommitState.Pending, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
}
