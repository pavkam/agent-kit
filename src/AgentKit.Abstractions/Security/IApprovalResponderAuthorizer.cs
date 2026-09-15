// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Authorizes an authenticated responder to resolve one exact approval request.</summary>
public interface IApprovalResponderAuthorizer
{
    /// <summary>Evaluates responder authority independently from channel authentication.</summary>
    /// <param name="request">The retained approval request.</param>
    /// <param name="candidateResponse">The structurally validated candidate response.</param>
    /// <param name="cancellationToken">Cancels authorization.</param>
    /// <returns>A closed authorization result.</returns>
    public ValueTask<ApprovalResponderAuthorizationResult> AuthorizeAsync(
        ApprovalRequest request,
        ApprovalResponse candidateResponse,
        CancellationToken cancellationToken = default);
}
