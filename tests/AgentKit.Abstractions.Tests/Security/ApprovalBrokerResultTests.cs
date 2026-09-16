// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Security;

/// <summary>Verifies ApprovalBrokerResult derived behavior and contracts.</summary>
public sealed class ApprovalBrokerResultTests
{
    [Fact]
    public void ApprovalBrokerApproved_WhenResponseIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new ApprovalBrokerApproved(null!)).ParamName.ShouldBe("Response");

    [Fact]
    public void ApprovalBrokerApproved_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var response = SecurityAbstractionsTestData.Response();
        var result = new ApprovalBrokerApproved(response);
        result.Response.ShouldBe(response);
    }

    [Fact]
    public void ApprovalBrokerApproved_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ApprovalBrokerApproved(SecurityAbstractionsTestData.Response());
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void ApprovalBrokerDenied_WhenResponseIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new ApprovalBrokerDenied(null!)).ParamName.ShouldBe("Response");

    [Fact]
    public void ApprovalBrokerDenied_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var response = SecurityAbstractionsTestData.Response(ApprovalResolution.Denied);
        var result = new ApprovalBrokerDenied(response);
        result.Response.ShouldBe(response);
    }

    [Fact]
    public void ApprovalBrokerDenied_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ApprovalBrokerDenied(SecurityAbstractionsTestData.Response(ApprovalResolution.Denied));
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void ApprovalBrokerExpired_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ApprovalBrokerExpired();
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void ApprovalBrokerUnavailable_WhenSafeReasonIsBlank_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new ApprovalBrokerUnavailable(" ")).ParamName.ShouldBe("SafeReason");

    [Fact]
    public void ApprovalBrokerUnavailable_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var result = new ApprovalBrokerUnavailable("unavailable");
        result.SafeReason.ShouldBe("unavailable");
    }

    [Fact]
    public void ApprovalBrokerUnavailable_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ApprovalBrokerUnavailable("unavailable");
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
