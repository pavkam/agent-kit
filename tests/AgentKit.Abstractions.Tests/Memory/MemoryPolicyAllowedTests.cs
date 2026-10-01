// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Memory;

/// <summary>Verifies <see cref="MemoryPolicyAllowed"/> constraints.</summary>
public sealed class MemoryPolicyAllowedTests
{
    [Fact]
    public void Constructor_WhenPolicyIdIsSupplied_PreservesIt()
    {
        var decision = new MemoryPolicyAllowed(new ComponentId("p"));

        decision.PolicyId.ShouldBe(new ComponentId("p"));
        _ = decision.ShouldBeAssignableTo<MemoryPolicyDecision>();
    }

    [Fact]
    public void Constructor_WhenPolicyIdIsDefault_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new MemoryPolicyAllowed(default)).ParamName.ShouldBe("policyId");
}
