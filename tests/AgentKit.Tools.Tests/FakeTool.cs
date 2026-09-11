// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Tests;

/// <summary>A fully scriptable <see cref="ITool"/> test double.</summary>
internal sealed class FakeTool: ITool
{
    public required ToolDescriptor Descriptor { get; init; }

    public Func<ToolInvocationRequest, CancellationToken, Task<ToolInvocationResult>>? OnInvoke { get; init; }

    public List<ToolInvocationRequest> ReceivedRequests { get; } = [];

    public Task<ToolInvocationResult> InvokeAsync(ToolInvocationRequest request, CancellationToken cancellationToken = default)
    {
        ReceivedRequests.Add(request);

        return OnInvoke is not null
            ? OnInvoke(request, cancellationToken)
            : Task.FromResult(new ToolInvocationResult(
                new ToolCallOutcome(ToolCallOutcomeKind.Success, ToolTerminalStatus.Succeeded, SideEffectCertainty.DefinitelyPerformed, false, null, ExtensionData.Empty),
                []));
    }
}
