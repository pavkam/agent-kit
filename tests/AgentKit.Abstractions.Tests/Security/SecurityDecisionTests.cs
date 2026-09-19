// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Security;

/// <summary>Verifies SecurityDecision derived behavior and contracts.</summary>
public sealed class SecurityDecisionTests
{
    [Fact]
    public void SecurityAllowed_WhenGrantIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new SecurityAllowed(new SecurityRequestId(Guid.NewGuid()), new SecurityPolicyVersion(1), null!)).ParamName.ShouldBe("grant");

    [Fact]
    public void SecurityAllowed_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var requestId = new SecurityRequestId(Guid.NewGuid());
        var grant = SecurityAbstractionsTestData.Grant();
        var allowed = new SecurityAllowed(requestId, new SecurityPolicyVersion(1), grant);
        allowed.RequestId.ShouldBe(requestId);
        allowed.PolicyVersion.ShouldBe(new SecurityPolicyVersion(1));
        allowed.Grant.ShouldBe(grant);
    }

    [Fact]
    public void SecurityAllowed_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SecurityAllowed(new SecurityRequestId(Guid.NewGuid()), new SecurityPolicyVersion(1), SecurityAbstractionsTestData.Grant());
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void SecurityDenied_WhenDenialIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new SecurityDenied(new SecurityRequestId(Guid.NewGuid()), new SecurityPolicyVersion(1), null!)).ParamName.ShouldBe("denial");

    [Fact]
    public void SecurityDenied_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var requestId = new SecurityRequestId(Guid.NewGuid());
        var denial = new SecurityDenial("code", "message");
        var denied = new SecurityDenied(requestId, new SecurityPolicyVersion(1), denial);
        denied.RequestId.ShouldBe(requestId);
        denied.PolicyVersion.ShouldBe(new SecurityPolicyVersion(1));
        denied.Denial.ShouldBe(denial);
    }

    [Fact]
    public void SecurityDenied_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SecurityDenied(new SecurityRequestId(Guid.NewGuid()), new SecurityPolicyVersion(1), new SecurityDenial("code", "message"));
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void SecurityApprovalRequired_WhenApprovalIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new SecurityApprovalRequired(new SecurityRequestId(Guid.NewGuid()), new SecurityPolicyVersion(1), null!)).ParamName.ShouldBe("approval");

    [Fact]
    public void SecurityApprovalRequired_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var requestId = new SecurityRequestId(Guid.NewGuid());
        var approval = new ApprovalRequest(new ApprovalRequestId(Guid.NewGuid()), SecurityAbstractionsTestData.ScopeBinding(), "presentation", DateTimeOffset.UnixEpoch);
        var decision = new SecurityApprovalRequired(requestId, new SecurityPolicyVersion(1), approval);
        decision.RequestId.ShouldBe(requestId);
        decision.PolicyVersion.ShouldBe(new SecurityPolicyVersion(1));
        decision.Approval.ShouldBe(approval);
    }

    [Fact]
    public void SecurityApprovalRequired_With_WhenApplied_ProducesEqualCopy()
    {
        var approval = new ApprovalRequest(new ApprovalRequestId(Guid.NewGuid()), SecurityAbstractionsTestData.ScopeBinding(), "presentation", DateTimeOffset.UnixEpoch);
        var original = new SecurityApprovalRequired(new SecurityRequestId(Guid.NewGuid()), new SecurityPolicyVersion(1), approval);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
