// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent;

/// <summary>Approves every call without asking, for the non-interactive <c>--smoke-test</c> path.</summary>
internal sealed class AutoApprovePrompt: IApprovalPrompt
{
    /// <inheritdoc/>
    public Task<bool> ConfirmAsync(string toolName, string argumentsJson, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[auto-approved] {toolName} {argumentsJson}");
        return Task.FromResult(true);
    }
}
