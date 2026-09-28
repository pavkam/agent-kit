// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies <see cref="DurableOperationDeferred"/> behavior and contracts.</summary>
public sealed class DurableOperationDeferredTests
{
    [Fact]
    public void Constructor_WhenCertaintyIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new DurableOperationDeferred(
            DurabilityTestData.Binding(),
            DurabilityTestData.Now,
            (SideEffectCertainty) 99,
            DurabilityTestData.Token));

        exception.ParamName.ShouldBe("sideEffectCertainty");
    }

    [Fact]
    public void Constructor_WhenFencingTokenIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new DurableOperationDeferred(
            DurabilityTestData.Binding(),
            DurabilityTestData.Now,
            SideEffectCertainty.Unknown,
            default));

        exception.ParamName.ShouldBe("fencingToken");
    }

    [Fact]
    public void Constructor_WhenNoWaitDetailIsSupplied_LeavesBothConditionsAbsent()
    {
        var deferred = new DurableOperationDeferred(
            DurabilityTestData.Binding(),
            DurabilityTestData.Now,
            SideEffectCertainty.Unknown,
            DurabilityTestData.Token);

        deferred.NotBefore.ShouldBeNull();
        deferred.ExternalReference.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WhenHandedOff_RetainsBothTheInstantAndTheOwnerReference()
    {
        var notBefore = DurabilityTestData.Now.AddMinutes(5);
        var reference = DurabilityTestData.ExternalReference();

        var deferred = new DurableOperationDeferred(
            DurabilityTestData.Binding(),
            DurabilityTestData.Now,
            SideEffectCertainty.Unknown,
            DurabilityTestData.Token,
            notBefore,
            reference);

        deferred.NotBefore.ShouldBe(notBefore);
        deferred.ExternalReference.ShouldBe(reference);
    }
}
