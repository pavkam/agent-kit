// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Tests;

using AgentKit.Hooks;

/// <summary>Verifies when memory hooks dispatch and that every failure is reported as a fail-closed outcome.</summary>
public sealed class MemoryHookRunnerTests
{
    private sealed class NoopProposalHook: IBeforeMemoryProposalHook
    {
        public ValueTask InvokeAsync(BeforeMemoryProposalEventArgs args, HookInvocationContext context, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;
    }

    private sealed class CancellingProposalHook(CancellationTokenSource source): IBeforeMemoryProposalHook
    {
        public ValueTask InvokeAsync(BeforeMemoryProposalEventArgs args, HookInvocationContext context, CancellationToken cancellationToken = default)
        {
            source.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.CompletedTask;
        }
    }

    private static MemoryHookRunner Runner(MemoryHarness harness, bool dispatcher = true) => new(
        harness.Time,
        dispatcher ? harness.Provider.GetRequiredService<IHookDispatcher>() : null,
        dispatcher ? harness.Provider.GetRequiredService<IIdentifierGenerator<HookDispatchId>>() : null);

    private static MemoryHarness WithHooks() => MemoryHarness.Create(arrange: services => _ = services.AddAgentHooks());

    [Fact]
    public async Task IsActive_WhenContextDispatcherAndRegistrationExist_IsTrueOtherwiseFalse()
    {
        using var harness = WithHooks();
        var owner = MemoryTestData.NewOwner();
        await using var scope = await MemoryHookScope.OpenAsync(
            (MemoryHookScope.Register("p", AgentHookPointDefinitions.BeforeMemoryProposalRegistration), new NoopProposalHook()));
        var context = MemoryHookScope.Context(scope, harness, owner);

        Runner(harness).IsActive(context, AgentHookPoints.BeforeMemoryProposal).ShouldBeTrue();
        Runner(harness).IsActive(context, AgentHookPoints.BeforeMemoryWrite).ShouldBeFalse();
        Runner(harness).IsActive(hooks: null, AgentHookPoints.BeforeMemoryProposal).ShouldBeFalse();
        Runner(harness, dispatcher: false).IsActive(context, AgentHookPoints.BeforeMemoryProposal).ShouldBeFalse();
    }

    [Fact]
    public async Task DispatchAsync_WhenTheCallersDeadlineHasPassed_ReportsAFailedOutcomeInsteadOfThrowing()
    {
        using var harness = WithHooks();
        var owner = MemoryTestData.NewOwner();
        await using var scope = await MemoryHookScope.OpenAsync(
            (MemoryHookScope.Register("p", AgentHookPointDefinitions.BeforeMemoryProposalRegistration), new NoopProposalHook()));
        var context = MemoryHookScope.Context(scope, harness, owner);
        harness.Time.Advance(TimeSpan.FromMinutes(5));
        var proposal = MemoryTestData.Proposal(owner);

        var outcome = await Runner(harness).DispatchAsync(
            context,
            AgentHookPointDefinitions.BeforeMemoryProposal,
            dispatch => new BeforeMemoryProposalEventArgs(dispatch, proposal),
            TestContext.Current.CancellationToken);

        outcome.Failed.ShouldBeTrue();
        outcome.Args.ShouldBeNull();
    }

    [Fact]
    public async Task DispatchAsync_WhenTheCallerCancels_PropagatesTheCancellation()
    {
        using var harness = WithHooks();
        var owner = MemoryTestData.NewOwner();
        using var source = new CancellationTokenSource();
        await using var scope = await MemoryHookScope.OpenAsync(
            (MemoryHookScope.Register("p", AgentHookPointDefinitions.BeforeMemoryProposalRegistration), new CancellingProposalHook(source)));
        var context = MemoryHookScope.Context(scope, harness, owner);
        var proposal = MemoryTestData.Proposal(owner);

        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await Runner(harness).DispatchAsync(
            context,
            AgentHookPointDefinitions.BeforeMemoryProposal,
            dispatch => new BeforeMemoryProposalEventArgs(dispatch, proposal),
            source.Token));
    }

    [Fact]
    public void Constructor_WhenTimeProviderIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new MemoryHookRunner(null!)).ParamName.ShouldBe("time");
}
