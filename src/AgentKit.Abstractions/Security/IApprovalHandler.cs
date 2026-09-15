// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Dispatches a retained approval request through one trusted host channel.</summary>
public interface IApprovalHandler
{
    /// <summary>Attempts to obtain one terminal response without granting authority.</summary>
    /// <param name="request">The retained, exactly bound approval request.</param>
    /// <param name="cancellationToken">Cancels waiting for a response.</param>
    /// <returns>A closed responded or unavailable handler result.</returns>
    public ValueTask<ApprovalHandlerResult> TryResolveAsync(
        ApprovalRequest request,
        CancellationToken cancellationToken = default);
}
