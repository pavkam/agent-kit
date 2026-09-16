// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Security;

/// <summary>Verifies ApprovalRequest behavior and contracts.</summary>
public sealed class ApprovalRequestTests
{
    [Fact]
    public void Constructor_WhenIdIsDefault_ThrowsExactArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ApprovalRequest(default, SecurityAbstractionsTestData.ScopeBinding(), "presentation", DateTimeOffset.UnixEpoch));

    [Fact]
    public void Constructor_WhenBindingIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new ApprovalRequest(new ApprovalRequestId(Guid.NewGuid()), null!, "presentation", DateTimeOffset.UnixEpoch)).ParamName.ShouldBe("binding");

    [Fact]
    public void Constructor_WhenSafePresentationIsBlank_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new ApprovalRequest(new ApprovalRequestId(Guid.NewGuid()), SecurityAbstractionsTestData.ScopeBinding(), " ", DateTimeOffset.UnixEpoch)).ParamName.ShouldBe("safePresentation");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var id = new ApprovalRequestId(Guid.NewGuid());
        var binding = SecurityAbstractionsTestData.ScopeBinding();
        var request = new ApprovalRequest(id, binding, "presentation", DateTimeOffset.UnixEpoch);
        request.Id.ShouldBe(id);
        request.Binding.ShouldBe(binding);
        request.SafePresentation.ShouldBe("presentation");
        request.CreatedAt.ShouldBe(DateTimeOffset.UnixEpoch);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ApprovalRequest(new ApprovalRequestId(Guid.NewGuid()), SecurityAbstractionsTestData.ScopeBinding(), "presentation", DateTimeOffset.UnixEpoch);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
