// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Plan.Tests;

/// <summary>Verifies GuidSecurityEnforcementIntentIdGenerator behavior and contracts.</summary>
public sealed class GuidSecurityEnforcementIntentIdGeneratorTests
{
    [Fact]
    public void Create_WhenCalledTwice_ReturnsDistinctNonDefaultIdentities()
    {
        var generator = new GuidSecurityEnforcementIntentIdGenerator();
        var first = generator.Create();
        var second = generator.Create();
        first.ShouldNotBe(default);
        second.ShouldNotBe(default);
        first.ShouldNotBe(second);
    }
}
