// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies <see cref="DurableBackendCapabilities"/> behavior and contracts.</summary>
public sealed class DurableBackendCapabilitiesTests
{
    [Fact]
    public void Constructor_WhenArgumentsAreSupplied_RoundTripsProperties()
    {
        var capabilities = new DurableBackendCapabilities(
            SupportsDistributedOwnership: true,
            SupportsExternalHandoff: false,
            SupportsReconciliation: true);

        capabilities.SupportsDistributedOwnership.ShouldBeTrue();
        capabilities.SupportsExternalHandoff.ShouldBeFalse();
        capabilities.SupportsReconciliation.ShouldBeTrue();
    }

    [Fact]
    public void Default_WhenUnset_ClaimsNoCapability()
    {
        DurableBackendCapabilities capabilities = default;

        capabilities.SupportsDistributedOwnership.ShouldBeFalse();
        capabilities.SupportsExternalHandoff.ShouldBeFalse();
        capabilities.SupportsReconciliation.ShouldBeFalse();
    }

    [Fact]
    public void Equals_WhenOneFlagDiffers_ReturnsFalse()
    {
        var capabilities = new DurableBackendCapabilities(true, true, true);

        capabilities.ShouldNotBe(capabilities with { SupportsExternalHandoff = false });
    }
}
