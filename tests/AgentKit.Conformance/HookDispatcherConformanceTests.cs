// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

/// <summary>Defines reusable kernel-dispatch requirements for <see cref="IHookDispatcher"/> implementations.</summary>
/// <typeparam name="TFixture">The implementation fixture.</typeparam>
public abstract class HookDispatcherConformanceTests<TFixture>
    where TFixture : IHookDispatcherConformanceFixture, new()
{
    /// <summary>Creates an isolated fixture.</summary>
    protected virtual TFixture CreateFixture() => new();

    /// <summary>Verifies three hooks observe mutations in deterministic registration order.</summary>
    [Fact]
    public async Task DispatchAsync_WhenThreeHooksRegistered_RunsInDeterministicOrder()
    {
        var fixture = CreateFixture();
        var order = await fixture.DispatchOrderedMutatingHooksAsync(TestContext.Current.CancellationToken);

        order.Count.ShouldBe(3);
        order.Distinct().Count().ShouldBe(3);
    }

    /// <summary>Verifies one dispatch mints a distinct invocation identity per hook execution.</summary>
    [Fact]
    public async Task DispatchAsync_WhenThreeHooksRun_MintsDistinctInvocationIds()
    {
        var fixture = CreateFixture();
        var invocationIds = await fixture.DispatchAndCollectInvocationIdsAsync(TestContext.Current.CancellationToken);

        invocationIds.Count.ShouldBe(3);
        invocationIds.Distinct().Count().ShouldBe(3);
        invocationIds.ShouldAllBe(static id => id != default);
    }

    /// <summary>Verifies closed point identity is enforced before hook resolution.</summary>
    [Fact]
    public async Task DispatchAsync_WhenPointIdentityMismatch_FailsBeforeDispatch()
    {
        var fixture = CreateFixture();
        await fixture.AssertPointMismatchFailsBeforeDispatchAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies an isolated observer rolls back mutable state before later hooks run.</summary>
    [Fact]
    public async Task DispatchAsync_WhenObserverThrows_IsolatedMutationDoesNotLeak()
    {
        var fixture = CreateFixture();
        var payload = await fixture.DispatchIsolatedObserverAsync(TestContext.Current.CancellationToken);
        payload.ShouldBe("baseline");
    }
}
