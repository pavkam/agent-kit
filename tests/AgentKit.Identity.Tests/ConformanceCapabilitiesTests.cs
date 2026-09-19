// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Identity.Tests;

/// <summary>Verifies <see cref="ConformanceCapabilities"/>.</summary>
public sealed class ConformanceCapabilitiesTests
{
    [Fact]
    public void Constructor_WhenFlagsAreOmitted_RequiresEveryOptionalBehavior()
    {
        var capabilities = new ConformanceCapabilities();
        capabilities.SupportsDurability.ShouldBeTrue();
        capabilities.SupportsConcurrentCreators.ShouldBeTrue();
    }

    [Fact]
    public void Constructor_WhenOptionalBehaviorIsUnsupported_PreservesThatDeclaration()
    {
        var capabilities = new ConformanceCapabilities(supportsDurability: false, supportsConcurrentCreators: false);
        capabilities.SupportsDurability.ShouldBeFalse();
        capabilities.SupportsConcurrentCreators.ShouldBeFalse();
        capabilities.ShouldBe(new ConformanceCapabilities(false, false));
    }
}
