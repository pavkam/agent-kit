// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies the journaled run-settlement manifest's argument constraints and value semantics.</summary>
public sealed class DurableRunSettlementManifestTests
{
    private static readonly Guid RunId = Guid.Parse("23000000-0000-0000-0000-000000000001");

    [Fact]
    public void Constructor_WhenRunIdIsEmpty_ThrowsForTheRunIdArgument() =>
        Should.Throw<ArgumentOutOfRangeException>(
                () => new DurableRunSettlementManifest(Guid.Empty, 2, "RunSucceeded"))
            .ParamName.ShouldBe("runId");

    [Fact]
    public void Constructor_WhenMessageCountIsNegative_ThrowsForTheMessageCountArgument() =>
        Should.Throw<ArgumentOutOfRangeException>(
                () => new DurableRunSettlementManifest(RunId, -1, "RunSucceeded"))
            .ParamName.ShouldBe("messageCount");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_WhenOutcomeKindIsBlank_ThrowsForTheOutcomeKindArgument(string? outcomeKind) =>
        Should.Throw<ArgumentException>(() => new DurableRunSettlementManifest(RunId, 2, outcomeKind!))
            .ParamName.ShouldBe("outcomeKind");

    [Fact]
    public void Constructor_WhenTheRunCommittedNothing_AcceptsAZeroCount() =>
        new DurableRunSettlementManifest(RunId, 0, "RunCancelled").MessageCount.ShouldBe(0);

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsEveryProperty()
    {
        var manifest = new DurableRunSettlementManifest(RunId, 4, "RunSucceeded");

        manifest.RunId.ShouldBe(RunId);
        manifest.MessageCount.ShouldBe(4);
        manifest.OutcomeKind.ShouldBe("RunSucceeded");
    }

    [Fact]
    public void Equals_WhenEveryFieldAgrees_IsStructural() =>
        new DurableRunSettlementManifest(RunId, 4, "RunSucceeded")
            .ShouldBe(new DurableRunSettlementManifest(RunId, 4, "RunSucceeded"));
}
