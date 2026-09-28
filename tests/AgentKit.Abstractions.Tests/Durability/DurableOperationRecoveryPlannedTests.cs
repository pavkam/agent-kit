// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies <see cref="DurableOperationRecoveryPlanned"/> behavior and contracts.</summary>
public sealed class DurableOperationRecoveryPlannedTests
{
    [Fact]
    public void Constructor_WhenActionIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new DurableOperationRecoveryPlanned(
            DurabilityTestData.Binding(),
            DurabilityTestData.Now,
            (DurableRecoveryAction) 99,
            DurableOperationState.EffectPending,
            SideEffectCertainty.Unknown));

        exception.ParamName.ShouldBe("action");
    }

    [Fact]
    public void Constructor_WhenEvidenceStateIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new DurableOperationRecoveryPlanned(
            DurabilityTestData.Binding(),
            DurabilityTestData.Now,
            DurableRecoveryAction.Reconcile,
            (DurableOperationState) 99,
            SideEffectCertainty.Unknown));

        exception.ParamName.ShouldBe("evidenceState");
    }

    [Fact]
    public void Constructor_WhenCertaintyIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new DurableOperationRecoveryPlanned(
            DurabilityTestData.Binding(),
            DurabilityTestData.Now,
            DurableRecoveryAction.Reconcile,
            DurableOperationState.EffectPending,
            (SideEffectCertainty) 99));

        exception.ParamName.ShouldBe("sideEffectCertainty");
    }

    [Theory]
    [InlineData(DurableRecoveryAction.Start)]
    [InlineData(DurableRecoveryAction.Reconcile)]
    [InlineData(DurableRecoveryAction.Retry)]
    [InlineData(DurableRecoveryAction.CommitRecordedResult)]
    [InlineData(DurableRecoveryAction.RequireOperator)]
    [InlineData(DurableRecoveryAction.NotPossible)]
    public void Constructor_WhenActionIsDefined_RoundTripsBoundedDimensions(DurableRecoveryAction action)
    {
        var planned = new DurableOperationRecoveryPlanned(
            DurabilityTestData.Binding(),
            DurabilityTestData.Now,
            action,
            DurableOperationState.EffectPending,
            SideEffectCertainty.Unknown);

        planned.Action.ShouldBe(action);
        planned.EvidenceState.ShouldBe(DurableOperationState.EffectPending);
        planned.SideEffectCertainty.ShouldBe(SideEffectCertainty.Unknown);
    }
}
