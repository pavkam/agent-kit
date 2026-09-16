// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies RecoveryEvidenceNotFound behavior and contracts.</summary>
public sealed class RecoveryEvidenceNotFoundTests
{
    [Fact]
    public void RecoveryEvidenceNotFound_Constructor_WhenAddressIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new RecoveryEvidenceNotFound(null!));
        exception.ParamName.ShouldBe("address");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsAddress()
    {
        var address = DurabilityTestData.Address();
        var notFound = new RecoveryEvidenceNotFound(address);
        notFound.Address.ShouldBeSameAs(address);
        RecoveryEvidenceResult result = notFound;
        _ = result.ShouldBeOfType<RecoveryEvidenceNotFound>();
    }

    [Fact]
    public void With_WhenAddressIsNull_ThrowsArgumentNullException()
    {
        var notFound = new RecoveryEvidenceNotFound(DurabilityTestData.Address());
        Should.Throw<ArgumentNullException>(() => _ = notFound with { Address = null! }).ParamName.ShouldBe("Address");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new RecoveryEvidenceNotFound(DurabilityTestData.Address());
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void With_WhenAddressIsValid_UpdatesAddress()
    {
        var original = new RecoveryEvidenceNotFound(DurabilityTestData.Address());
        var newAddress = DurabilityTestData.Address();
        var updated = original with { Address = newAddress };
        updated.Address.ShouldBeSameAs(newAddress);
    }
}
