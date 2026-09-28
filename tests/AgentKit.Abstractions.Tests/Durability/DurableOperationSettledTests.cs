// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies <see cref="DurableOperationSettled"/> behavior and contracts.</summary>
public sealed class DurableOperationSettledTests
{
    [Fact]
    public void Constructor_WhenStateIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new DurableOperationSettled(
            DurabilityTestData.Binding(),
            DurabilityTestData.Now,
            (DurableOperationState) 99,
            SideEffectCertainty.DefinitelyPerformed,
            DurabilityTestData.Token));

        exception.ParamName.ShouldBe("state");
    }

    [Fact]
    public void Constructor_WhenCertaintyIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new DurableOperationSettled(
            DurabilityTestData.Binding(),
            DurabilityTestData.Now,
            DurableOperationState.Completed,
            (SideEffectCertainty) 99,
            DurabilityTestData.Token));

        exception.ParamName.ShouldBe("sideEffectCertainty");
    }

    [Fact]
    public void Constructor_WhenFencingTokenIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new DurableOperationSettled(
            DurabilityTestData.Binding(),
            DurabilityTestData.Now,
            DurableOperationState.Completed,
            SideEffectCertainty.DefinitelyPerformed,
            default));

        exception.ParamName.ShouldBe("fencingToken");
    }

    [Fact]
    public void Constructor_WhenOutcomeIsStagedButUnpublished_ReportsOutcomeReady()
    {
        var settled = new DurableOperationSettled(
            DurabilityTestData.Binding(),
            DurabilityTestData.Now,
            DurableOperationState.OutcomeReady,
            SideEffectCertainty.DefinitelyPerformed,
            DurabilityTestData.Token);

        settled.State.ShouldBe(DurableOperationState.OutcomeReady);
        settled.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyPerformed);
        settled.FencingToken.ShouldBe(DurabilityTestData.Token);
    }
}
