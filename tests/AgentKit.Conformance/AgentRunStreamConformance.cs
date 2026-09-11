// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

using AgentKit.TestSupport;

/// <summary>Verifies the shared event-subscription and final-result ownership contract against replaceable stream adapters.</summary>
public abstract class AgentRunStreamConformance
{
    /// <summary>Creates one independent deterministic fixture using RunResultTestData coordinates.</summary><returns>A fixture with an already registered non-owning stream and no completed result.</returns>
    protected abstract AgentRunStreamFixture CreateFixture();

    /// <summary>Verifies registration-before-enumeration, event order and exact final-envelope identity.</summary><returns>The asynchronous conformance check.</returns>
    [Fact]
    public async Task ReadAllAsync_WhenEventsPrecedeEnumeration_DrainsInOrderAndRetainsIdenticalCompletion()
    {
        await using var fixture = CreateFixture();
        var first = RunResultTestData.Event(1); var second = RunResultTestData.Event(2);
        await fixture.PublishAsync(first, TestContext.Current.CancellationToken);
        await fixture.PublishAsync(second, TestContext.Current.CancellationToken);
        var result = RunResultTestData.Finished();
        await fixture.CompleteAsync(result, TestContext.Current.CancellationToken);
        List<RunEvent> events = [];
        await foreach (var item in fixture.Stream.ReadAllAsync(TestContext.Current.CancellationToken)) { events.Add(item); }
        events.ShouldBe([first, second]);
        (await fixture.Stream.Completion).ShouldBeSameAs(result);
        (await fixture.Stream.Completion).ShouldBeSameAs(result);
        fixture.Stream.AgentId.ShouldBe(result.AgentId); fixture.Stream.SessionId.ShouldBe(result.SessionId);
        fixture.Stream.ConversationId.ShouldBe(result.ConversationId); fixture.Stream.RunId.ShouldBe(result.RunId);
    }

    /// <summary>Verifies that cancellation of a waiting reader never cancels the accepted run's final result.</summary><returns>The asynchronous conformance check.</returns>
    [Fact]
    public async Task ReadAllAsync_WhenCancelled_LeavesCompletionIndependent()
    {
        await using var fixture = CreateFixture();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await using var reader = fixture.Stream.ReadAllAsync(cancellation.Token).GetAsyncEnumerator(cancellation.Token);
        var pending = reader.MoveNextAsync().AsTask();
        pending.IsCompleted.ShouldBeFalse(); cancellation.Cancel();
        _ = await Should.ThrowAsync<OperationCanceledException>(() => pending);
        fixture.Stream.Completion.IsCompleted.ShouldBeFalse();
        var result = RunResultTestData.Finished();
        await fixture.CompleteAsync(result, TestContext.Current.CancellationToken);
        (await fixture.Stream.Completion).ShouldBeSameAs(result);
    }

    /// <summary>Verifies repeated local disposal cannot complete or cancel producer-owned work.</summary><returns>The asynchronous conformance check.</returns>
    [Fact]
    public async Task DisposeAsync_WhenCalledBeforeSettlement_PreservesCompletionWaiting()
    {
        await using var fixture = CreateFixture();
        await fixture.Stream.DisposeAsync(); await fixture.Stream.DisposeAsync();
        fixture.Stream.Completion.IsCompleted.ShouldBeFalse();
        var result = RunResultTestData.Finished(settlement: new RunSettlementRecoveryRequired(RunResultTestData.Error(AgentErrorCodes.StoreUnavailable)));
        await fixture.CompleteAsync(result, TestContext.Current.CancellationToken);
        var completed = await fixture.Stream.Completion;
        completed.ShouldBeSameAs(result); completed.IsCleanSuccess.ShouldBeFalse();
        _ = completed.Outcome.ShouldBeOfType<RunSucceeded>(); completed.Output.ShouldBe("output");
    }

    /// <summary>Verifies a second enumerator cannot steal the first reader's delivery.</summary><returns>The asynchronous conformance check.</returns>
    [Fact]
    public async Task ReadAllAsync_WhenSecondReaderStarts_RejectsWithoutCancellingFirst()
    {
        await using var fixture = CreateFixture();
        await using var first = fixture.Stream.ReadAllAsync(TestContext.Current.CancellationToken).GetAsyncEnumerator(TestContext.Current.CancellationToken);
        await using var second = fixture.Stream.ReadAllAsync(TestContext.Current.CancellationToken).GetAsyncEnumerator(TestContext.Current.CancellationToken);
        var pending = first.MoveNextAsync().AsTask();
        _ = await Should.ThrowAsync<InvalidOperationException>(() => second.MoveNextAsync().AsTask());
        var item = RunResultTestData.Event(1); await fixture.PublishAsync(item, TestContext.Current.CancellationToken);
        (await pending).ShouldBeTrue(); first.Current.ShouldBeSameAs(item);
        await fixture.CompleteAsync(RunResultTestData.Finished(), TestContext.Current.CancellationToken);
        (await first.MoveNextAsync()).ShouldBeFalse();
    }
}
