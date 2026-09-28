// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies <see cref="DurableReconciled"/> behavior and contracts.</summary>
public sealed class DurableReconciledTests
{
    [Fact]
    public void Constructor_WhenCertaintyIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new DurableReconciled((SideEffectCertainty) 99));

        exception.ParamName.ShouldBe("sideEffectCertainty");
    }

    [Theory]
    [InlineData(SideEffectCertainty.DefinitelyNotPerformed)]
    [InlineData(SideEffectCertainty.Unknown)]
    [InlineData(SideEffectCertainty.DefinitelyPerformed)]
    [InlineData(SideEffectCertainty.PartiallyPerformed)]
    [InlineData(SideEffectCertainty.NotApplicable)]
    public void Constructor_WhenCertaintyIsDefined_RetainsItAsAReconciliationResult(SideEffectCertainty certainty)
    {
        var reconciled = new DurableReconciled(certainty);

        reconciled.SideEffectCertainty.ShouldBe(certainty);
        _ = reconciled.ShouldBeAssignableTo<DurableReconciliationResult>();
    }

    [Fact]
    public void Init_WhenCertaintyIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        var reconciled = new DurableReconciled(SideEffectCertainty.Unknown);

        _ = Should.Throw<ArgumentOutOfRangeException>(() => reconciled with { SideEffectCertainty = (SideEffectCertainty) 99 });
    }
}
