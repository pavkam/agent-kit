// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies <see cref="DurableOperationWaiting"/> behavior and contracts.</summary>
public sealed class DurableOperationWaitingTests
{
    [Fact]
    public void Constructor_WhenBindingIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new DurableOperationWaiting(
            null!,
            DurabilityTestData.Token,
            DurabilityTestData.Now,
            SideEffectCertainty.Unknown,
            notBefore: DurabilityTestData.Now.AddMinutes(1)));

        exception.ParamName.ShouldBe("binding");
    }

    [Fact]
    public void Constructor_WhenFencingTokenIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var binding = new DurableOperationBinding(DurabilityTestData.Address(), DurabilityTestData.Context());
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new DurableOperationWaiting(
            binding,
            default,
            DurabilityTestData.Now,
            SideEffectCertainty.Unknown,
            notBefore: DurabilityTestData.Now.AddMinutes(1)));

        exception.ParamName.ShouldBe("fencingToken");
    }

    [Fact]
    public void Constructor_WhenNoWaitConditionSupplied_ThrowsArgumentOutOfRangeException()
    {
        var binding = new DurableOperationBinding(DurabilityTestData.Address(), DurabilityTestData.Context());
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new DurableOperationWaiting(
            binding,
            DurabilityTestData.Token,
            DurabilityTestData.Now,
            SideEffectCertainty.Unknown));

        exception.ParamName.ShouldBe("notBefore");
    }

    [Fact]
    public void Constructor_WhenExternalReferenceSupplied_RoundTripsDerivedProperties()
    {
        var binding = new DurableOperationBinding(DurabilityTestData.Address(), DurabilityTestData.Context());
        var reference = DurabilityTestData.ExternalReference();
        var waiting = new DurableOperationWaiting(
            binding,
            DurabilityTestData.Token,
            DurabilityTestData.Now,
            SideEffectCertainty.Unknown,
            externalReference: reference);

        waiting.Binding.ShouldBe(binding);
        waiting.Address.ShouldBe(binding.Address);
        waiting.ExecutionContext.ShouldBe(binding.ExecutionContext);
        waiting.ExternalReference.ShouldBe(reference);
        waiting.NotBefore.ShouldBeNull();
    }
}
