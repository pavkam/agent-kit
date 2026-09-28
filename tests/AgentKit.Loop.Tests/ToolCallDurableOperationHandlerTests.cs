// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop.Tests;

using AgentKit.Conformance;

/// <summary>Verifies the tool-call handler claims its own boundary and bridges to a live invocation.</summary>
public sealed class ToolCallDurableOperationHandlerTests
{
    private static readonly DurableJournalKey JournalKey = new("loop-journal");

    /// <summary>Verifies the registry is required, because the handler has no other way to reach a live boundary.</summary>
    [Fact]
    public void Constructor_WhenRegistryIsNull_ThrowsForTheRegistryArgument() =>
        Should.Throw<ArgumentNullException>(() => new ToolCallDurableOperationHandler(null!))
            .ParamName.ShouldBe("registry");

    /// <summary>Verifies the handler claims exactly the tool-call boundary.</summary>
    [Fact]
    public void OperationName_WhenConstructed_IsTheToolCallBoundary() =>
        new ToolCallDurableOperationHandler(new DurableBoundaryRegistry())
            .OperationName.ShouldBe(LoopDurableOperations.ToolCall);

    /// <summary>Verifies a published continuation runs and its terminal result is returned unchanged.</summary>
    [Fact]
    public async Task InvokeAsync_WhenAContinuationIsPublished_ReturnsItsTerminalResult()
    {
        var registry = new DurableBoundaryRegistry();
        var handler = new ToolCallDurableOperationHandler(registry);
        var operation = DurabilityConformanceData.Descriptor(JournalKey) with { Name = LoopDurableOperations.ToolCall };
        var lease = new StubExecutionLease(operation.Address, new FencingToken(9));
        var writer = new StubCheckpointWriter(operation, lease.FencingToken);
        var context = new DurableInvocationContext(operation, lease, writer);
        using var published = registry.Register(operation.Address.OperationId, (_, _) =>
            ValueTask.FromResult(new DurableOperationResult(
                operation.Binding,
                DurableOperationState.Completed,
                SideEffectCertainty.DefinitelyPerformed,
                operation.Input,
                lease.FencingToken,
                DurabilityConformanceData.Now)));

        var result = await handler.InvokeAsync(context, TestContext.Current.CancellationToken);

        result.FencingToken.ShouldBe(new FencingToken(9));
        result.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyPerformed);
    }
}
