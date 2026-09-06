// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction.Tests;

using Microsoft.Extensions.Options;

public sealed class CharacterCompactionSizeEstimatorTests
{
    [Fact]
    public void Constructor_WhenOptionsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new CharacterCompactionSizeEstimator(null!));

        exception.ParamName.ShouldBe("options");
    }

    [Fact]
    public void EstimateEntries_WhenEntriesDefault_ThrowsArgumentException()
    {
        var estimator = CreateEstimator();

        var exception = Should.Throw<ArgumentException>(() => estimator.EstimateEntries(default));

        exception.ParamName.ShouldBe("entries");
    }

    [Fact]
    public void EstimateEntries_WhenEntriesEmpty_ReturnsZeroEstimate()
    {
        var estimator = CreateEstimator();

        var estimate = estimator.EstimateEntries([]);

        estimate.Tokens.ShouldBe(0);
        estimate.Bytes.ShouldBe(0);
        estimate.EntryCount.ShouldBe(0);
    }

    [Fact]
    public void EstimateEntries_WhenGivenTextEntries_ComputesByteAndTokenCounts()
    {
        var estimator = CreateEstimator(charactersPerToken: 4.0);
        var address = new SessionAddress(new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid()));
        var branchId = new BranchId(Guid.NewGuid());
        var entries = ImmutableArray.Create<SessionEntry>(
            TestFactory.MessageEntry(address, branchId, 1, "12345678"));

        var estimate = estimator.EstimateEntries(entries);

        estimate.Bytes.ShouldBe(8);
        estimate.Tokens.ShouldBe(2);
        estimate.EntryCount.ShouldBe(1);
    }

    [Fact]
    public void EstimateCheckpoint_WhenCheckpointNull_ThrowsArgumentNullException()
    {
        var estimator = CreateEstimator();

        var exception = Should.Throw<ArgumentNullException>(() => estimator.EstimateCheckpoint(null!));

        exception.ParamName.ShouldBe("checkpoint");
    }

    [Fact]
    public void EstimateCheckpoint_WhenGivenSummary_ComputesEstimate()
    {
        var estimator = CreateEstimator(charactersPerToken: 4.0);
        var checkpoint = new CompactionCheckpoint(
            [new TextPart("12345678", TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty);

        var estimate = estimator.EstimateCheckpoint(checkpoint);

        estimate.Bytes.ShouldBe(8);
        estimate.Tokens.ShouldBe(2);
        estimate.EntryCount.ShouldBe(1);
    }

    private static CharacterCompactionSizeEstimator CreateEstimator(double charactersPerToken = 4.0) =>
        new(Options.Create(new CompactionOptions { CharactersPerToken = charactersPerToken }));
}
