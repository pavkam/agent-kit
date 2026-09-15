// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions;

/// <summary>Fails closed when a host has not configured responder authorization.</summary>
internal sealed class DenyApprovalResponderAuthorizer: IApprovalResponderAuthorizer
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
        return ValueTask.FromResult<ApprovalResponderAuthorizationResult>(
            new ApprovalResponderUnauthorized("No approval responder authorizer is configured."));
    }
}
