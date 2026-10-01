// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Memory;

/// <summary>Verifies <see cref="MemoryPolicyDenied"/> constraints.</summary>
public sealed class MemoryPolicyDeniedTests
{
    [Fact]
    public void Constructor_WhenValid_PreservesEveryValue()
    {
        var decision = new MemoryPolicyDenied(new ComponentId("p"), "code", "message");

        decision.PolicyId.ShouldBe(new ComponentId("p"));
        decision.Code.ShouldBe("code");
        decision.SafeMessage.ShouldBe("message");
        _ = decision.ShouldBeAssignableTo<MemoryPolicyDecision>();
    }

    [Fact]
    public void Constructor_WhenPolicyIdIsDefault_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new MemoryPolicyDenied(default, "c", "m")).ParamName.ShouldBe("policyId");

    [Fact]
    public void Constructor_WhenCodeOrMessageIsBlank_ThrowsArgumentException()
    {
        Should.Throw<ArgumentException>(() => new MemoryPolicyDenied(new ComponentId("p"), " ", "m")).ParamName.ShouldBe("code");
        Should.Throw<ArgumentException>(() => new MemoryPolicyDenied(new ComponentId("p"), "c", " ")).ParamName.ShouldBe("safeMessage");
    }
}
