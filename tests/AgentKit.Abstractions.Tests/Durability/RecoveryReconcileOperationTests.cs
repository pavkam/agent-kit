// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies RecoveryReconcileOperation behavior and contracts.</summary>
public sealed class RecoveryReconcileOperationTests
{
    [Fact]
    public void RecoveryReconcileOperation_Constructor_WhenReferenceIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new RecoveryReconcileOperation(null!));
        exception.ParamName.ShouldBe("reference");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsReference()
    {
        var reference = DurabilityTestData.ExternalReference();
        var operation = new RecoveryReconcileOperation(reference);
        operation.Reference.ShouldBeSameAs(reference);
        RecoveryDecision typed = operation;
        _ = typed.ShouldBeOfType<RecoveryReconcileOperation>();
    }

    [Fact]
    public void With_WhenReferenceIsNull_ThrowsArgumentNullException()
    {
        var operation = new RecoveryReconcileOperation(DurabilityTestData.ExternalReference());
        Should.Throw<ArgumentNullException>(() => _ = operation with { Reference = null! }).ParamName.ShouldBe("Reference");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new RecoveryReconcileOperation(DurabilityTestData.ExternalReference());
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void With_WhenReferenceIsValid_UpdatesReference()
    {
        var original = new RecoveryReconcileOperation(DurabilityTestData.ExternalReference());
        var newReference = DurabilityTestData.ExternalReference();
        var updated = original with { Reference = newReference };
        updated.Reference.ShouldBeSameAs(newReference);
    }
}
