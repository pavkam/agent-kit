// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Tests;

/// <summary>Verifies the fail-closed default responder authorizer used when no authority is configured.</summary>
public sealed class DenyApprovalResponderAuthorizerTests
{
    [Fact]
    public async Task AuthorizeAsync_WhenCalled_ReturnsUnauthorized()
    {
        var authorizer = new DenyApprovalResponderAuthorizer();
        var request = SecurityAuthorityTestData.CreateApprovalRequest();
        var response = new ApprovalResponse(
            new ApprovalResponseId(Guid.Parse("51000000-0000-0000-0000-000000000005")),
            request.Id,
            request.Binding,
            ApprovalResolution.Approved,
            request.Binding.Request.Identity,
            request.CreatedAt.AddSeconds(1));

        var result = await authorizer.AuthorizeAsync(request, response, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ApprovalResponderUnauthorized>().SafeReason
            .ShouldBe("No approval responder authorizer is configured.");
    }

    [Fact]
    public async Task AuthorizeAsync_WhenRequestIsNull_ThrowsArgumentNullException()
    {
        var authorizer = new DenyApprovalResponderAuthorizer();
        var request = SecurityAuthorityTestData.CreateApprovalRequest();
        var response = new ApprovalResponse(
            new ApprovalResponseId(Guid.Parse("51000000-0000-0000-0000-000000000005")),
            request.Id,
            request.Binding,
            ApprovalResolution.Approved,
            request.Binding.Request.Identity,
            request.CreatedAt.AddSeconds(1));

        var exception = await Should.ThrowAsync<ArgumentNullException>(
            () => authorizer.AuthorizeAsync(null!, response, TestContext.Current.CancellationToken).AsTask());
        exception.ParamName.ShouldBe("request");
    }

    [Fact]
    public async Task AuthorizeAsync_WhenCandidateResponseIsNull_ThrowsArgumentNullException()
    {
        var authorizer = new DenyApprovalResponderAuthorizer();
        var request = SecurityAuthorityTestData.CreateApprovalRequest();

        var exception = await Should.ThrowAsync<ArgumentNullException>(
            () => authorizer.AuthorizeAsync(request, null!, TestContext.Current.CancellationToken).AsTask());
        exception.ParamName.ShouldBe("candidateResponse");
    }

    [Fact]
    public async Task AuthorizeAsync_WhenCancellationIsRequested_ThrowsOperationCanceledException()
    {
        var authorizer = new DenyApprovalResponderAuthorizer();
        var request = SecurityAuthorityTestData.CreateApprovalRequest();
        var response = new ApprovalResponse(
            new ApprovalResponseId(Guid.Parse("51000000-0000-0000-0000-000000000005")),
            request.Id,
            request.Binding,
            ApprovalResolution.Approved,
            request.Binding.Request.Identity,
            request.CreatedAt.AddSeconds(1));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => authorizer.AuthorizeAsync(request, response, cts.Token).AsTask());
    }
}
