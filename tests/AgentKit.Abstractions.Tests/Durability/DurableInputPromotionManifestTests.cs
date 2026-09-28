// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies the journaled input-promotion manifest's argument constraints and value semantics.</summary>
public sealed class DurableInputPromotionManifestTests
{
    private static readonly Guid RunId = Guid.Parse("21000000-0000-0000-0000-000000000001");
    private static readonly Guid TurnId = Guid.Parse("22000000-0000-0000-0000-000000000001");

    [Fact]
    public void Constructor_WhenRunIdIsEmpty_ThrowsForTheRunIdArgument() =>
        Should.Throw<ArgumentOutOfRangeException>(
                () => new DurableInputPromotionManifest(Guid.Empty, TurnId, "AfterTurnCommitted", 1))
            .ParamName.ShouldBe("runId");

    [Fact]
    public void Constructor_WhenTargetTurnIdIsEmpty_ThrowsForTheTargetTurnIdArgument() =>
        Should.Throw<ArgumentOutOfRangeException>(
                () => new DurableInputPromotionManifest(RunId, Guid.Empty, "AfterTurnCommitted", 1))
            .ParamName.ShouldBe("targetTurnId");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WhenBoundaryIsBlank_ThrowsForTheBoundaryArgument(string? boundary) =>
        Should.Throw<ArgumentException>(() => new DurableInputPromotionManifest(RunId, TurnId, boundary!, 1))
            .ParamName.ShouldBe("boundary");

    [Fact]
    public void Constructor_WhenPromotedMessageCountIsNegative_ThrowsForThatArgument() =>
        Should.Throw<ArgumentOutOfRangeException>(
                () => new DurableInputPromotionManifest(RunId, TurnId, "AfterTurnCommitted", -1))
            .ParamName.ShouldBe("promotedMessageCount");

    [Fact]
    public void Constructor_WhenTheAttemptHasNotCommitted_AcceptsAZeroCount()
    {
        var manifest = new DurableInputPromotionManifest(RunId, TurnId, "BeforeFirstModelRequest", 0);

        manifest.PromotedMessageCount.ShouldBe(0);
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsEveryProperty()
    {
        var manifest = new DurableInputPromotionManifest(RunId, TurnId, "OtherwiseIdle", 3);

        manifest.RunId.ShouldBe(RunId);
        manifest.TargetTurnId.ShouldBe(TurnId);
        manifest.Boundary.ShouldBe("OtherwiseIdle");
        manifest.PromotedMessageCount.ShouldBe(3);
    }

    [Fact]
    public void Equals_WhenEveryFieldAgrees_IsStructural() =>
        new DurableInputPromotionManifest(RunId, TurnId, "OtherwiseIdle", 3)
            .ShouldBe(new DurableInputPromotionManifest(RunId, TurnId, "OtherwiseIdle", 3));
}
