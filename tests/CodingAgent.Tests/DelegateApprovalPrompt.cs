// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent.Tests;

using AgentKit;

/// <summary>Supplies deterministic approval prompt behavior to adapter tests.</summary>
internal sealed class DelegateApprovalPrompt(
    Func<ApprovalRequest, CancellationToken, Task<bool>> callback): IApprovalPrompt
{
    public Task<bool> ConfirmAsync(ApprovalRequest request, CancellationToken cancellationToken) =>
        callback(request, cancellationToken);
}
