// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Tests;

using AgentKit.TestSupport;

/// <summary>Verifies <see cref="ToolInvokerBridge"/> maps spec contexts onto legacy <see cref="ITool"/> implementations.</summary>
public sealed class ToolInvokerBridgeTests
{
    [Fact]
    public async Task InvokeAsync_WhenToolSucceeds_ReturnsToolResult()
    {
        var tool = new RecordingTool(ToolCaptureTestData.Descriptor());
        var bridge = new ToolInvokerBridge(tool);
        var context = ToolCaptureTestData.InvocationContext(tool.Descriptor);

        var result = await bridge.InvokeAsync(context, TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Success);
        tool.Invocations.ShouldBe(1);
    }

    [Fact]
    public async Task InvokeAsync_WhenContextIsNull_ThrowsArgumentNullException()
    {
        var bridge = new ToolInvokerBridge(new RecordingTool(ToolCaptureTestData.Descriptor()));
        var exception = await Should.ThrowAsync<ArgumentNullException>(
            () => bridge.InvokeAsync(null!, TestContext.Current.CancellationToken).AsTask());
        exception.ParamName.ShouldBe("context");
    }

    private sealed class RecordingTool(ToolDescriptor descriptor): ITool
    {
        public int Invocations { get; private set; }

        public ToolDescriptor Descriptor => descriptor;

        public Task<ToolInvocationResult> InvokeAsync(ToolInvocationRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            Invocations++;
            return Task.FromResult(new ToolInvocationResult(
                new ToolCallOutcome(
                    ToolCallOutcomeKind.Success,
                    ToolTerminalStatus.Succeeded,
                    SideEffectCertainty.DefinitelyPerformed,
                    false,
                    null,
                    ExtensionData.Empty),
                []));
        }
    }
}
