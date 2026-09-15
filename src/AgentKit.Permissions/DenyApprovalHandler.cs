// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions;

/// <summary>Fails closed when a host has not configured an approval channel.</summary>
internal sealed class DenyApprovalHandler: IApprovalHandler
{
    /// <inheritdoc/>
    public ValueTask<ApprovalHandlerResult> TryResolveAsync(ApprovalRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<ApprovalHandlerResult>(
            new ApprovalHandlerUnavailable("No approval handler is configured."));
    }
}
