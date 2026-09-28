// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies <see cref="DurableBackendSelected"/> behavior and contracts.</summary>
public sealed class DurableBackendSelectedTests
{
    [Fact]
    public void Constructor_WhenDescriptorIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new DurableBackendSelected(null!));

        exception.ParamName.ShouldBe("descriptor");
    }

    [Fact]
    public void Constructor_WhenDescriptorIsSupplied_RetainsItAsASelectionResult()
    {
        var descriptor = DurabilityTestData.BackendDescriptor();

        var selected = new DurableBackendSelected(descriptor);

        selected.Descriptor.ShouldBe(descriptor);
        _ = selected.ShouldBeAssignableTo<DurableBackendSelectionResult>();
    }

    [Fact]
    public void Equals_WhenTheSameDescriptorIsSelected_ReturnsTrue()
    {
        var descriptor = DurabilityTestData.BackendDescriptor();

        new DurableBackendSelected(descriptor).ShouldBe(new DurableBackendSelected(descriptor));
    }

    [Fact]
    public void Equals_WhenDifferentDescriptorsAreSelected_ReturnsFalse()
    {
        var descriptor = DurabilityTestData.BackendDescriptor();
        var other = descriptor with { Key = new DurableBackendKey("other") };

        new DurableBackendSelected(other).ShouldNotBe(new DurableBackendSelected(descriptor));
    }
}
