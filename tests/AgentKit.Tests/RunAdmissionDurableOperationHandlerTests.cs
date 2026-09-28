// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

using AgentKit.Conformance;

/// <summary>Verifies the run-admission handler bridges to a live continuation and refuses when none exists.</summary>
public sealed class RunAdmissionDurableOperationHandlerTests
{
    private static readonly DurableJournalKey JournalKey = new("engine-journal");

    /// <summary>Verifies the registry is required, because the handler has no other way to reach a live boundary.</summary>
    [Fact]
    public void Constructor_WhenRegistryIsNull_ThrowsForTheRegistryArgument() =>
        Should.Throw<ArgumentNullException>(() => new RunAdmissionDurableOperationHandler(null!))
            .ParamName.ShouldBe("registry");

    /// <summary>Verifies the handler claims exactly the run-admission boundary.</summary>
    [Fact]
    public void OperationName_WhenConstructed_IsTheRunAdmissionBoundary() =>
        new RunAdmissionDurableOperationHandler(new DurableBoundaryRegistry())
            .OperationName.ShouldBe(EngineDurableOperations.RunAdmission);

    /// <summary>Verifies a published continuation receives the accepted declaration and the acquired lease.</summary>
    [Fact]
    public async Task InvokeAsync_WhenAContinuationIsPublished_RunsItUnderTheLease()
    {
        var registry = new DurableBoundaryRegistry();
        var handler = new RunAdmissionDurableOperationHandler(registry);
        var operation = Descriptor();
        var lease = new StubExecutionLease(operation.Address, new FencingToken(2));
        var context = new DurableInvocationContext(
            operation, lease, new StubCheckpointWriter(operation, lease.FencingToken));
        RecoverableOperationDescriptor? observed = null;
        using var published = registry.Register(operation.Address.OperationId, (invocation, _) =>
        {
            observed = invocation.Operation;
            return ValueTask.FromResult(Completed(invocation.Operation, invocation.Lease));
        });

        var result = await handler.InvokeAsync(context, TestContext.Current.CancellationToken);

        observed.ShouldBe(operation);
        result.FencingToken.ShouldBe(new FencingToken(2));
        result.State.ShouldBe(DurableOperationState.Completed);
    }

    /// <summary>Verifies a recovering process refuses rather than inventing a terminal record for work it never ran.</summary>
    [Fact]
    public async Task InvokeAsync_WhenNoContinuationIsPublished_Throws()
    {
        var handler = new RunAdmissionDurableOperationHandler(new DurableBoundaryRegistry());
        var operation = Descriptor();
        var lease = new StubExecutionLease(operation.Address, new FencingToken(1));
        var context = new DurableInvocationContext(
            operation, lease, new StubCheckpointWriter(operation, lease.FencingToken));

        _ = await Should.ThrowAsync<InvalidOperationException>(() => handler
            .InvokeAsync(context, TestContext.Current.CancellationToken).AsTask());
    }

    /// <summary>Verifies a null context is refused before the registry is consulted.</summary>
    [Fact]
    public async Task InvokeAsync_WhenContextIsNull_ThrowsForTheContextArgument() =>
        (await Should.ThrowAsync<ArgumentNullException>(() =>
            new RunAdmissionDurableOperationHandler(new DurableBoundaryRegistry())
                .InvokeAsync(null!, TestContext.Current.CancellationToken)
                .AsTask())).ParamName.ShouldBe("context");

    private static RecoverableOperationDescriptor Descriptor() =>
        DurabilityConformanceData.Descriptor(JournalKey) with { Name = EngineDurableOperations.RunAdmission };

    private static DurableOperationResult Completed(
        RecoverableOperationDescriptor declaration, IExecutionLease lease) => new(
        declaration.Binding,
        DurableOperationState.Completed,
        SideEffectCertainty.DefinitelyPerformed,
        declaration.Input,
        lease.FencingToken,
        DurabilityConformanceData.Now);
}
