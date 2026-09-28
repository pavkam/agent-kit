// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies the journaled compaction-activation manifest's argument constraints and value semantics.</summary>
public sealed class DurableCompactionActivationManifestTests
{
    private static readonly Guid CompactionId = Guid.Parse("24000000-0000-0000-0000-000000000001");

    [Fact]
    public void Constructor_WhenCompactionIdIsEmpty_ThrowsForTheCompactionIdArgument() =>
        Should.Throw<ArgumentOutOfRangeException>(
                () => new DurableCompactionActivationManifest(Guid.Empty, 4, 5, 10))
            .ParamName.ShouldBe("compactionId");

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void Constructor_WhenSourceVersionIsNotPositive_ThrowsForTheSourceVersionArgument(long sourceVersion) =>
        Should.Throw<ArgumentOutOfRangeException>(
                () => new DurableCompactionActivationManifest(CompactionId, sourceVersion, 5, 10))
            .ParamName.ShouldBe("sourceVersion");

    [Theory]
    [InlineData(4L)]
    [InlineData(3L)]
    public void Constructor_WhenActivatedVersionDoesNotFollowTheSource_ThrowsForThatArgument(long activatedVersion) =>
        Should.Throw<ArgumentOutOfRangeException>(
                () => new DurableCompactionActivationManifest(CompactionId, 4, activatedVersion, 10))
            .ParamName.ShouldBe("activatedVersion");

    [Fact]
    public void Constructor_WhenCoveredEntryCountIsNegative_ThrowsForThatArgument() =>
        Should.Throw<ArgumentOutOfRangeException>(
                () => new DurableCompactionActivationManifest(CompactionId, 4, 5, -1))
            .ParamName.ShouldBe("coveredEntryCount");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsEveryProperty()
    {
        var manifest = new DurableCompactionActivationManifest(CompactionId, 4, 5, 10);

        manifest.CompactionId.ShouldBe(CompactionId);
        manifest.SourceVersion.ShouldBe(4);
        manifest.ActivatedVersion.ShouldBe(5);
        manifest.CoveredEntryCount.ShouldBe(10);
    }

    [Fact]
    public void Equals_WhenEveryFieldAgrees_IsStructural() =>
        new DurableCompactionActivationManifest(CompactionId, 4, 5, 10)
            .ShouldBe(new DurableCompactionActivationManifest(CompactionId, 4, 5, 10));
}
