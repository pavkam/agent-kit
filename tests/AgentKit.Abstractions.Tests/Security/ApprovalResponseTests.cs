// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Security;

/// <summary>Verifies ApprovalResponse behavior and contracts.</summary>
public sealed class ApprovalResponseTests
{
    [Fact]
    public void Constructor_WhenIdIsDefault_ThrowsExactArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ApprovalResponse(default, new ApprovalRequestId(Guid.NewGuid()), SecurityAbstractionsTestData.ScopeBinding(), ApprovalResolution.Approved, SecurityAbstractionsTestData.Identity(), DateTimeOffset.UnixEpoch));

    [Fact]
    public void Constructor_WhenRequestIdIsDefault_ThrowsExactArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ApprovalResponse(new ApprovalResponseId(Guid.NewGuid()), default, SecurityAbstractionsTestData.ScopeBinding(), ApprovalResolution.Approved, SecurityAbstractionsTestData.Identity(), DateTimeOffset.UnixEpoch));

    [Fact]
    public void Constructor_WhenBindingIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new ApprovalResponse(new ApprovalResponseId(Guid.NewGuid()), new ApprovalRequestId(Guid.NewGuid()), null!, ApprovalResolution.Approved, SecurityAbstractionsTestData.Identity(), DateTimeOffset.UnixEpoch)).ParamName.ShouldBe("binding");

    [Fact]
    public void Constructor_WhenResolutionIsUndefined_ThrowsExactArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ApprovalResponse(new ApprovalResponseId(Guid.NewGuid()), new ApprovalRequestId(Guid.NewGuid()), SecurityAbstractionsTestData.ScopeBinding(), (ApprovalResolution) 99, SecurityAbstractionsTestData.Identity(), DateTimeOffset.UnixEpoch)).ParamName.ShouldBe("resolution");

    [Fact]
    public void Constructor_WhenApproverIdentityIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new ApprovalResponse(new ApprovalResponseId(Guid.NewGuid()), new ApprovalRequestId(Guid.NewGuid()), SecurityAbstractionsTestData.ScopeBinding(), ApprovalResolution.Approved, null!, DateTimeOffset.UnixEpoch)).ParamName.ShouldBe("approverIdentity");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var response = SecurityAbstractionsTestData.Response();
        response.Binding.ShouldBe(SecurityAbstractionsTestData.ScopeBinding());
        response.Resolution.ShouldBe(ApprovalResolution.Approved);
        response.ApproverIdentity.ShouldBe(SecurityAbstractionsTestData.Identity());
        response.RespondedAt.ShouldBe(DateTimeOffset.UnixEpoch);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = SecurityAbstractionsTestData.Response();
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
