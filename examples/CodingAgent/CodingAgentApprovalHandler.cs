// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent;

/// <summary>Translates one exact retained approval request into an authenticated terminal UI response.</summary>
internal sealed class CodingAgentApprovalHandler(
    IApprovalPrompt prompt,
    ExecutionIdentity identity,
    TimeProvider timeProvider): IApprovalHandler
{
    /// <inheritdoc/>
    public async ValueTask<ApprovalHandlerResult> TryResolveAsync(
        ApprovalRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var approved = timeProvider.GetUtcNow() < request.Binding.ExpiresAt
            && await prompt.ConfirmAsync(request, cancellationToken).ConfigureAwait(false);
        var respondedAt = timeProvider.GetUtcNow();
        var response = new ApprovalResponse(
            new ApprovalResponseId(Guid.NewGuid()),
            request.Id,
            request.Binding,
            approved && respondedAt < request.Binding.ExpiresAt
                ? ApprovalResolution.Approved
                : ApprovalResolution.Denied,
            identity,
            respondedAt);
        return new ApprovalHandlerResponded(response);
    }
}
