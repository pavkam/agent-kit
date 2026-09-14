// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent;

/// <summary>Asks whether one mutating tool call may proceed, before it runs.</summary>
internal interface IApprovalPrompt
{
    /// <summary>Requests approval for one tool call.</summary>
    /// <param name="toolName">The tool's display name.</param>
    /// <param name="argumentsJson">The call's raw JSON arguments.</param>
    /// <param name="cancellationToken">Cancels the pending approval, which denies the call.</param>
    /// <returns><see langword="true"/> if the call may proceed; <see langword="false"/> to deny it.</returns>
    public Task<bool> ConfirmAsync(string toolName, string argumentsJson, CancellationToken cancellationToken);
}
