// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Tests;

using AgentKit.TestSupport;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using static ToolRuntimeTestCalls;

/// <summary>Verifies <see cref="ToolEventDispatcher"/> ordering, isolation, timeout, cancellation, and signal safety.</summary>
public sealed class ToolEventDispatcherTests
{
    [Fact]
    public async Task PublishAsync_WhenSeveralSinksAreRegistered_DeliversInOrderThenIdentity()
    {
        var order = new List<string>();
        var dispatcher = Dispatcher(
            logger: null,
            Binding("c", 1, new RecordingSink(order, "c")),
            Binding("b", 0, new RecordingSink(order, "b")),
            Binding("a", 0, new RecordingSink(order, "a")));

        await dispatcher.PublishAsync(Event(), TestContext.Current.CancellationToken);

        order.ShouldBe(["a", "b", "c"]);
    }

    [Fact]
    public async Task PublishAsync_WhenASinkThrows_IsolatesItAndStillDeliversToLaterSinks()
    {
        var order = new List<string>();
        var logger = new RecordingLogger<ToolEventDispatcher>();
        var dispatcher = Dispatcher(
            logger,
            Binding("a", 0, new FaultingSink("sink secret detail")),
            Binding("b", 1, new RecordingSink(order, "b")));
        using var metrics = new MetricCollector(AgentKitMetricNames.ToolEventPublishCount);

        await dispatcher.PublishAsync(Event(), TestContext.Current.CancellationToken);

        order.ShouldBe(["b"]);
        var failure = logger.Snapshot().ShouldHaveSingleItem();
        failure.EventId.Id.ShouldBe(4130);
        failure.Level.ShouldBe(LogLevel.Warning);
        failure.Message.ShouldContain("InvalidOperationException");
        SignalAssertions.ShouldNotContainContent([], logger.Snapshot(), metrics.Snapshot(), "secret detail");
        metrics.Snapshot().ShouldContain(static observation => Equals(observation.Tags[AgentKitTagNames.Outcome], "failed"));
        metrics.Snapshot().ShouldContain(static observation => Equals(observation.Tags[AgentKitTagNames.Outcome], "delivered"));
    }

    [Fact]
    public async Task PublishAsync_WhenASinkThrowsSynchronously_IsolatesIt()
    {
        var dispatcher = Dispatcher(logger: null, Binding("a", 0, new SynchronousThrowingSink()));

        await dispatcher.PublishAsync(Event(), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task PublishAsync_WhenASinkIgnoresCancellationAndHangs_AbandonsItAfterTheTimeout()
    {
        var fake = new FakeTimeProvider();
        var order = new List<string>();
        var logger = new RecordingLogger<ToolEventDispatcher>();
        var dispatcher = Dispatcher(
            logger,
            fake,
            new ToolRuntimeOptions { EventSinkTimeout = TimeSpan.FromSeconds(1) },
            Binding("a", 0, new HangingSink()),
            Binding("b", 1, new RecordingSink(order, "b")));

        var task = dispatcher.PublishAsync(Event(), TestContext.Current.CancellationToken).AsTask();
        while (!task.IsCompleted)
        {
            TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
            fake.Advance(TimeSpan.FromSeconds(1));
            await Task.Yield();
        }

        await task;
        order.ShouldBe(["b"]);
        logger.Snapshot().ShouldHaveSingleItem().Message.ShouldContain("Timeout");
    }

    [Fact]
    public async Task PublishAsync_WhenCallerCancels_PropagatesCancellationAndSkipsLaterSinks()
    {
        var order = new List<string>();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var dispatcher = Dispatcher(
            logger: null,
            Binding("a", 0, new CancellingSink(cancellation)),
            Binding("b", 1, new RecordingSink(order, "b")));

        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await dispatcher.PublishAsync(Event(), cancellation.Token));

        order.ShouldBeEmpty();
    }

    [Fact]
    public async Task PublishAsync_WhenCallerIsAlreadyCancelled_ThrowsBeforeAnyDelivery()
    {
        var order = new List<string>();
        var dispatcher = Dispatcher(logger: null, Binding("a", 0, new RecordingSink(order, "a")));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await dispatcher.PublishAsync(Event(), cancellation.Token));

        order.ShouldBeEmpty();
    }

    [Fact]
    public async Task PublishAsync_WhenNoSinkIsRegistered_CompletesWithoutEffect()
    {
        var dispatcher = Dispatcher(logger: null);

        await dispatcher.PublishAsync(Event(), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task PublishAsync_WhenEventIsNull_ThrowsExactParameter()
    {
        var dispatcher = Dispatcher(logger: null);

        (await Should.ThrowAsync<ArgumentNullException>(async () => await dispatcher.PublishAsync(null!))).ParamName.ShouldBe("toolEvent");
    }

    [Fact]
    public void Constructor_WhenDependencyIsNull_ThrowsExactParameter()
    {
        var options = Options.Create(new ToolRuntimeOptions());
        var logger = new RecordingLogger<ToolEventDispatcher>();

        Should.Throw<ArgumentNullException>(() => new ToolEventDispatcher(null!, options, TimeProvider.System, logger)).ParamName.ShouldBe("bindings");
        Should.Throw<ArgumentNullException>(() => new ToolEventDispatcher([], null!, TimeProvider.System, logger)).ParamName.ShouldBe("options");
        Should.Throw<ArgumentNullException>(() => new ToolEventDispatcher([], options, null!, logger)).ParamName.ShouldBe("timeProvider");
        Should.Throw<ArgumentNullException>(() => new ToolEventDispatcher([], options, TimeProvider.System, null!)).ParamName.ShouldBe("logger");
        Should.Throw<ArgumentNullException>(() => new ToolEventDispatcher([null!], options, TimeProvider.System, logger)).ParamName.ShouldBe("bindings");
    }

    private static ToolEventDispatcher Dispatcher(ILogger<ToolEventDispatcher>? logger, params ToolEventSinkBinding[] bindings) =>
        Dispatcher(logger, TimeProvider.System, new ToolRuntimeOptions(), bindings);

    private static ToolEventDispatcher Dispatcher(
        ILogger<ToolEventDispatcher>? logger,
        TimeProvider timeProvider,
        ToolRuntimeOptions options,
        params ToolEventSinkBinding[] bindings) =>
        new(bindings, Options.Create(options), timeProvider, logger ?? new RecordingLogger<ToolEventDispatcher>());

    private static ToolEventSinkBinding Binding(string id, int order, IToolEventSink sink) =>
        new(new ToolEventSinkRegistration(new ComponentId(id), order), sink);

    private static ToolCallAcceptedEvent Event() => new(
        AgentId, SessionId, RunId, TurnId, OperationId, new ToolCallId(Guid.Parse("66666666-6666-6666-6666-666666666661")),
        DateTimeOffset.UnixEpoch, new ToolId("tool.read"), new ToolVersion("1"), Standard);

    private sealed class RecordingSink(List<string> order, string name): IToolEventSink
    {
        public ValueTask PublishAsync(ToolEvent toolEvent, CancellationToken cancellationToken = default)
        {
            order.Add(name);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FaultingSink(string message): IToolEventSink
    {
        public async ValueTask PublishAsync(ToolEvent toolEvent, CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            throw new InvalidOperationException(message);
        }
    }

    private sealed class SynchronousThrowingSink: IToolEventSink
    {
        public ValueTask PublishAsync(ToolEvent toolEvent, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("sync failure");
    }

    private sealed class HangingSink: IToolEventSink
    {
        public ValueTask PublishAsync(ToolEvent toolEvent, CancellationToken cancellationToken = default) =>
            new(new TaskCompletionSource().Task);
    }

    private sealed class CancellingSink(CancellationTokenSource source): IToolEventSink
    {
        public async ValueTask PublishAsync(ToolEvent toolEvent, CancellationToken cancellationToken = default)
        {
            await source.CancelAsync();
            cancellationToken.ThrowIfCancellationRequested();
        }
    }
}
