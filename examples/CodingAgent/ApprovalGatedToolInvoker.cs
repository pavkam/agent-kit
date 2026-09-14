// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent;

using AgentKit.Tools.Command;
using AgentKit.Tools.Edit;
using AgentKit.Tools.Write;

/// <summary>An <see cref="IToolInvoker"/> decorator that asks for approval before a mutating tool call runs.</summary>
/// <remarks>
/// Every other tool call is delegated to <paramref name="inner"/> unchanged. This exists because
/// <c>ToolAuthorizationRequest</c> carries only a tool's identity, never its call arguments — approving or
/// denying a specific write/edit/command invocation needs the arguments, which only <see cref="ToolCallRequest"/>
/// itself carries, so this decorates the invoker rather than <c>IToolAuthorizer</c>.
/// </remarks>
internal sealed class ApprovalGatedToolInvoker(IToolInvoker inner, IApprovalPrompt approvals): IToolInvoker
{
    private static readonly ImmutableHashSet<ToolId> _gatedToolIds =
        [WriteFileTool.Id, EditTool.Id, CommandTool.Id];

    /// <inheritdoc/>
    public async Task<ToolInvocationResult> InvokeAsync(ToolCallRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_gatedToolIds.Contains(request.ToolId))
        {
            return await inner.InvokeAsync(request, cancellationToken).ConfigureAwait(false);
        }

        var approved = await approvals.ConfirmAsync(
            request.ToolId.Value, request.Arguments.GetRawText(), cancellationToken).ConfigureAwait(false);
        return approved
            ? await inner.InvokeAsync(request, cancellationToken).ConfigureAwait(false)
            : new ToolInvocationResult(
                new ToolCallOutcome(
                    ToolCallOutcomeKind.Rejected,
                    ToolTerminalStatus.Denied,
                    SideEffectCertainty.DefinitelyNotPerformed,
                    retryable: true,
                    "The user denied this action.",
                    ExtensionData.Empty),
                []);
    }
}
