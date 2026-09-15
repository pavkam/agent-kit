// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent.Tests;

using AgentKit;

/// <summary>Verifies the local terminal accepts only responses from its exact authenticated human.</summary>
public sealed class CodingAgentApprovalResponderAuthorizerTests
{
    [Fact]
    public async Task AuthorizeAsync_WhenResponseMatchesRequestAndIdentity_Authorizes()
    {
        var identity = ApprovalTestData.Identity();
        var request = ApprovalTestData.Request(identity);
        var response = Response(request, identity);
        var authorizer = new CodingAgentApprovalResponderAuthorizer(identity);

        var result = await authorizer.AuthorizeAsync(
            request,
            response,
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<ApprovalResponderAuthorized>();
    }

    [Fact]
    public async Task AuthorizeAsync_WhenResponderIdentityDiffers_Denies()
    {
        var identity = ApprovalTestData.Identity();
        var request = ApprovalTestData.Request(identity);
        var response = Response(request, ApprovalTestData.Identity("other-operator"));
        var authorizer = new CodingAgentApprovalResponderAuthorizer(identity);

        var result = await authorizer.AuthorizeAsync(
            request,
            response,
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<ApprovalResponderUnauthorized>();
    }

    private static ApprovalResponse Response(ApprovalRequest request, ExecutionIdentity identity) => new(
        new ApprovalResponseId(Guid.Parse("60000000-0000-0000-0000-000000000006")),
        request.Id,
        request.Binding,
        ApprovalResolution.Approved,
        identity,
        ApprovalTestData.Now.AddSeconds(1));
}
