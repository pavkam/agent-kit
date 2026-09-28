// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies <see cref="DurableDispatched"/> behavior and contracts.</summary>
public sealed class DurableDispatchedTests
{
    [Fact]
    public void Constructor_WhenExternalReferenceIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new DurableDispatched(null!));

        exception.ParamName.ShouldBe("externalReference");
    }

    [Fact]
    public void Constructor_WhenReferenceIsSupplied_RetainsItAsADispatchResult()
    {
        var reference = DurabilityTestData.ExternalReference();

        var dispatched = new DurableDispatched(reference);

        dispatched.ExternalReference.ShouldBe(reference);
        _ = dispatched.ShouldBeAssignableTo<DurableDispatchResult>();
    }

    [Fact]
    public void Equals_WhenReferencesMatch_ReturnsTrue()
    {
        var first = new DurableDispatched(DurabilityTestData.ExternalReference());
        var second = new DurableDispatched(DurabilityTestData.ExternalReference());

        second.ShouldBe(first);
    }
}
