// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent;

/// <summary>Approves every call without asking, for the non-interactive <c>--smoke-test</c> path.</summary>
internal sealed class AutoApprovePrompt: IApprovalPrompt
{
    /// <inheritdoc/>
    public Task<bool> ConfirmAsync(ApprovalRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        Console.WriteLine($"[auto-approved] {request.SafePresentation}");
        return Task.FromResult(true);
    }
}
