// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent;

/// <summary>Accepts responses only from the exact human identity bound to this local terminal session.</summary>
internal sealed class CodingAgentApprovalResponderAuthorizer(ExecutionIdentity identity): IApprovalResponderAuthorizer
{
    /// <inheritdoc/>
    public ValueTask<ApprovalResponderAuthorizationResult> AuthorizeAsync(
        ApprovalRequest request,
        ApprovalResponse candidateResponse,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(candidateResponse);
        cancellationToken.ThrowIfCancellationRequested();
        var exact = request.Binding.Request.Identity == identity
            && candidateResponse.ApproverIdentity == identity
            && candidateResponse.RequestId == request.Id
            && candidateResponse.Binding == request.Binding;
        return ValueTask.FromResult<ApprovalResponderAuthorizationResult>(exact
            ? new ApprovalResponderAuthorized()
            : new ApprovalResponderUnauthorized("The response does not belong to the authenticated local terminal identity."));
    }
}
