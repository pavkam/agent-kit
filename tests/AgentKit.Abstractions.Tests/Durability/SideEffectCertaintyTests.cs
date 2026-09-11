// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies SideEffectCertainty behavior and contracts.</summary>
public sealed class SideEffectCertaintyTests
{
    [Fact]
    public void SideEffectCertainty_WhenCanonicalValues_AreWireStable()
    {
        ((int) SideEffectCertainty.DefinitelyNotPerformed).ShouldBe(0);
        ((int) SideEffectCertainty.Unknown).ShouldBe(1);
        ((int) SideEffectCertainty.DefinitelyPerformed).ShouldBe(2);
        ((int) SideEffectCertainty.PartiallyPerformed).ShouldBe(3);
        ((int) SideEffectCertainty.NotApplicable).ShouldBe(4);
    }
}
