// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop.Tests;

using AgentKit.Conformance;

/// <summary>Verifies the model-attempt handler bridges to a live continuation and refuses when none exists.</summary>
public sealed class ModelRequestDurableOperationHandlerTests
{
    private static readonly DurableJournalKey JournalKey = new("loop-journal");

    /// <summary>Verifies the registry is required, because the handler has no other way to reach a live boundary.</summary>
    [Fact]
    public void Constructor_WhenRegistryIsNull_ThrowsForTheRegistryArgument() =>
        Should.Throw<ArgumentNullException>(() => new ModelRequestDurableOperationHandler(null!))
            .ParamName.ShouldBe("registry");

    /// <summary>Verifies the handler claims exactly the model-attempt boundary.</summary>
    [Fact]
    public void OperationName_WhenConstructed_IsTheModelRequestBoundary() =>
        new ModelRequestDurableOperationHandler(new DurableBoundaryRegistry())
            .OperationName.ShouldBe(LoopDurableOperations.ModelRequest);

    /// <summary>Verifies a published continuation receives the accepted declaration and the acquired lease.</summary>
    [Fact]
    public async Task InvokeAsync_WhenAContinuationIsPublished_RunsItUnderTheLease()
    {
        var registry = new DurableBoundaryRegistry();
        var handler = new ModelRequestDurableOperationHandler(registry);
        var operation = Descriptor();
        var lease = new StubExecutionLease(operation.Address, new FencingToken(4));
        var writer = new StubCheckpointWriter(operation, lease.FencingToken);
        var context = new DurableInvocationContext(operation, lease, writer);
        RecoverableOperationDescriptor? observed = null;
        using var published = registry.Register(operation.Address.OperationId, (invocation, _) =>
        {
            observed = invocation.Operation;
            return ValueTask.FromResult(Completed(invocation.Operation, invocation.Lease));
        });

        var result = await handler.InvokeAsync(context, TestContext.Current.CancellationToken);

        observed.ShouldBe(operation);
        result.FencingToken.ShouldBe(new FencingToken(4));
        result.State.ShouldBe(DurableOperationState.Completed);
    }

    /// <summary>Verifies a recovering process refuses rather than inventing a terminal record for work it never ran.</summary>
    [Fact]
    public async Task InvokeAsync_WhenNoContinuationIsPublished_Throws()
    {
        var handler = new ModelRequestDurableOperationHandler(new DurableBoundaryRegistry());
        var operation = Descriptor();
        var lease = new StubExecutionLease(operation.Address, new FencingToken(1));
        var writer = new StubCheckpointWriter(operation, lease.FencingToken);
        var context = new DurableInvocationContext(operation, lease, writer);

        _ = await Should.ThrowAsync<InvalidOperationException>(() => handler
            .InvokeAsync(context, TestContext.Current.CancellationToken).AsTask());
    }

    /// <summary>Verifies a declaration for another boundary is refused, so one handler never answers for another.</summary>
    [Fact]
    public async Task InvokeAsync_WhenTheDeclarationNamesAnotherOperation_Throws()
    {
        var registry = new DurableBoundaryRegistry();
        var handler = new ToolCallDurableOperationHandler(registry);
        var operation = Descriptor();
        var lease = new StubExecutionLease(operation.Address, new FencingToken(1));
        var writer = new StubCheckpointWriter(operation, lease.FencingToken);
        var context = new DurableInvocationContext(operation, lease, writer);
        using var published = registry.Register(
            operation.Address.OperationId, (invocation, _) => ValueTask.FromResult(Completed(invocation.Operation, invocation.Lease)));

        _ = await Should.ThrowAsync<InvalidOperationException>(() => handler
            .InvokeAsync(context, TestContext.Current.CancellationToken).AsTask());
    }

    /// <summary>Verifies a null context is refused before the registry is consulted.</summary>
    [Fact]
    public async Task InvokeAsync_WhenContextIsNull_ThrowsForTheContextArgument() =>
        (await Should.ThrowAsync<ArgumentNullException>(() =>
            new ModelRequestDurableOperationHandler(new DurableBoundaryRegistry())
                .InvokeAsync(null!, TestContext.Current.CancellationToken)
                .AsTask())).ParamName.ShouldBe("context");

    private static RecoverableOperationDescriptor Descriptor() =>
        DurabilityConformanceData.Descriptor(JournalKey) with { Name = LoopDurableOperations.ModelRequest };

    private static DurableOperationResult Completed(
        RecoverableOperationDescriptor declaration, IExecutionLease lease) => new(
        declaration.Binding,
        DurableOperationState.Completed,
        SideEffectCertainty.DefinitelyPerformed,
        declaration.Input,
        lease.FencingToken,
        DurabilityConformanceData.Now);
}
