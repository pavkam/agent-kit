// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies the input and settlement boundary names and versions are stable configuration values.</summary>
/// <remarks>
/// These names appear in host profile configuration and in persisted operation records, so a rename breaks both.
/// Asserting the exact text is the cheapest way to make that visible in review.
/// </remarks>
public sealed class IoDurableOperationsTests
{
    [Fact]
    public void InputPromotion_WhenRead_IsTheStableBoundaryName() =>
        IoDurableOperations.InputPromotion.Value.ShouldBe("agentkit.io.input_promotion");

    [Fact]
    public void RunSettlement_WhenRead_IsTheStableBoundaryName() =>
        IoDurableOperations.RunSettlement.Value.ShouldBe("agentkit.io.run_settlement");

    [Fact]
    public void Versions_WhenRead_AreExplicitlyPublished()
    {
        IoDurableOperations.InputPromotionVersion.Value.ShouldBe("v1");
        IoDurableOperations.RunSettlementVersion.Value.ShouldBe("v1");
    }

    [Fact]
    public void All_WhenRead_ContainsEveryJournaledBoundaryExactlyOnce() =>
        IoDurableOperations.All.ShouldBe(
            [IoDurableOperations.InputPromotion, IoDurableOperations.RunSettlement], ignoreOrder: true);
}
