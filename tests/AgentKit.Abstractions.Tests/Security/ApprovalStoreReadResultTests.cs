// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Security;

/// <summary>Verifies ApprovalStoreReadResult behavior and contracts.</summary>
public sealed class ApprovalStoreReadResultTests
{
    [Fact]
    public void Constructor_WhenResponsePresentWithoutRequest_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new ApprovalStoreReadResult(null, SecurityAbstractionsTestData.Response())).ParamName.ShouldBe("request");

    [Fact]
    public void Constructor_WhenResponseTargetsAnotherRequest_ThrowsExactArgumentException()
    {
        var request = new ApprovalRequest(new ApprovalRequestId(Guid.NewGuid()), SecurityAbstractionsTestData.ScopeBinding(), "presentation", DateTimeOffset.UnixEpoch);
        var response = SecurityAbstractionsTestData.Response();
        _ = Should.Throw<ArgumentException>(() => new ApprovalStoreReadResult(request, response));
    }

    [Fact]
    public void Constructor_WhenNeitherIsPresent_RoundTripsNullProperties()
    {
        var result = new ApprovalStoreReadResult(null, null);
        result.Request.ShouldBeNull();
        result.Response.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WhenRequestAndMatchingResponseArePresent_RoundTripsProperties()
    {
        var response = SecurityAbstractionsTestData.Response();
        var request = new ApprovalRequest(response.RequestId, SecurityAbstractionsTestData.ScopeBinding(), "presentation", DateTimeOffset.UnixEpoch);
        var result = new ApprovalStoreReadResult(request, response);
        result.Request.ShouldBe(request);
        result.Response.ShouldBe(response);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ApprovalStoreReadResult(null, null);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
