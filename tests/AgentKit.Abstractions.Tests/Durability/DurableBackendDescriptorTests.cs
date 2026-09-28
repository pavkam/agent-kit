// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies <see cref="DurableBackendDescriptor"/> behavior and contracts.</summary>
public sealed class DurableBackendDescriptorTests
{
    [Fact]
    public void Constructor_WhenKeyIsDefault_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new DurableBackendDescriptor(
            default,
            default,
            [],
            supportsFencing: true,
            supportsReconciliation: true));

        exception.ParamName.ShouldBe("key");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var capabilities = new DurableBackendCapabilities(
            SupportsDistributedOwnership: true,
            SupportsExternalHandoff: false,
            SupportsReconciliation: true);
        var operations = ImmutableArray.Create(new DurableOperationName("tool.call"));

        var descriptor = new DurableBackendDescriptor(
            new DurableBackendKey("backend"),
            capabilities,
            operations,
            supportsFencing: true,
            supportsReconciliation: false);

        descriptor.Key.ShouldBe(new DurableBackendKey("backend"));
        descriptor.Capabilities.ShouldBe(capabilities);
        descriptor.SupportedOperations.ShouldBe(operations);
        descriptor.SupportsFencing.ShouldBeTrue();
        descriptor.SupportsReconciliation.ShouldBeFalse();
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var descriptor = DurabilityTestData.BackendDescriptor();

        (descriptor with { }).ShouldBe(descriptor);
    }

    [Fact]
    public void Equals_WhenCapabilitiesDiffer_ReturnsFalse()
    {
        var descriptor = DurabilityTestData.BackendDescriptor();
        var other = descriptor with { Capabilities = default };

        other.ShouldNotBe(descriptor);
    }
}
