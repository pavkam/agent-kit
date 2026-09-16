// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Security;

/// <summary>Verifies ApprovalStoreCapabilities behavior and contracts.</summary>
public sealed class ApprovalStoreCapabilitiesTests
{
    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var capabilities = new ApprovalStoreCapabilities(true, true);
        capabilities.IsDurable.ShouldBeTrue();
        capabilities.ProvidesTrustedControlPlane.ShouldBeTrue();
    }

    [Fact]
    public void Constructor_WhenTrustedControlPlaneOmitted_DefaultsToFalse()
    {
        var capabilities = new ApprovalStoreCapabilities(true);
        capabilities.ProvidesTrustedControlPlane.ShouldBeFalse();
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ApprovalStoreCapabilities(true, false);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
