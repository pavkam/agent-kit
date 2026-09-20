// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

using AgentKit.IO;
using AgentKit.TestSupport;

public sealed class AgentCancelAttachTests
{
    private static readonly ComponentKey<IInputCoordinator> InputKey = new("attach-input");
    private static readonly ComponentKey<IOutputPublisher> OutputKey = new("attach-output");

    [Fact]
    public async Task CancelAsync_WhenRunIsBlockedOnGate_SettlesCancelledAndReleasesLane()
    {
        var loop = new GatedAgentLoop { Gate = new TaskCompletionSource(), HonorDurableAbort = true };
        var sessions = new InMemoryTestSessionCoordinator();
        CompositionTestData.SeedSession(sessions, CompositionTestData.AgentId, CompositionTestData.SessionId);
        await using var engine = CompositionTestData.SendableBuilder(loop, sessions).Build();
        var agent = (await engine.GetAgentAsync(CompositionTestData.AgentId, TestContext.Current.CancellationToken)).RequireResolved();
        var identity = CompositionTestData.Identity();
        var running = agent.RunAsync<string>(
            CompositionTestData.SessionId,
            identity,
            CompositionTestData.Input(),
            options: CompositionTestData.RunOptions(),
            cancellationToken: TestContext.Current.CancellationToken);
        await loop.Entered.Task;
        var runId = loop.Requests.Single().RunId;

        _ = (await agent.CancelAsync(runId, identity, TestContext.Current.CancellationToken)).ShouldBeOfType<SessionRunAbortRecorded>();

        loop.Gate!.SetResult();
        var finished = (await running).ShouldBeOfType<AgentRunFinished<string>>();
        finished.Outcome.ShouldBeOfType<RunCancelled>();

        loop.Gate = new TaskCompletionSource();
        var second = agent.RunAsync<string>(
            CompositionTestData.SessionId,
            identity,
            CompositionTestData.Input(),
            options: CompositionTestData.RunOptions(),
            cancellationToken: TestContext.Current.CancellationToken);
        await loop.Entered.Task;
        loop.Gate.SetResult();
        _ = await second;
    }

    [Fact]
    public async Task AttachAsync_WhenRunHasSettled_ReturnsRejected()
    {
        var loop = new GatedAgentLoop();
        var sessions = new InMemoryTestSessionCoordinator();
        CompositionTestData.SeedSession(sessions, CompositionTestData.AgentId, CompositionTestData.SessionId);
        await using var engine = BuildStreamableEngine(loop, sessions);
        var agent = (await engine.GetAgentAsync(CompositionTestData.AgentId, TestContext.Current.CancellationToken)).RequireResolved();
        var identity = CompositionTestData.Identity();
        var runId = (await agent.RunAsync<string>(
            CompositionTestData.SessionId,
            identity,
            CompositionTestData.Input(),
            options: CompositionTestData.RunOptions(),
            cancellationToken: TestContext.Current.CancellationToken))
            .ShouldBeOfType<AgentRunFinished<string>>().RunId;

        var rejected = (await agent.AttachAsync<string>(runId, identity, TestContext.Current.CancellationToken))
            .ShouldBeOfType<AgentRunStreamRejected<string>>();
        rejected.Rejection.Failure.Code.ShouldBe(AgentErrorCodes.InvalidState);
    }

    [Fact]
    public async Task AttachAsync_WhenRunIsActive_ReceivesReplayAndLiveTail()
    {
        var loop = new GatedAgentLoop { Gate = new TaskCompletionSource(), HonorDurableAbort = true };
        var sessions = new InMemoryTestSessionCoordinator();
        CompositionTestData.SeedSession(sessions, CompositionTestData.AgentId, CompositionTestData.SessionId);
        await using var engine = BuildStreamableEngine(loop, sessions);
        var agent = (await engine.GetAgentAsync(CompositionTestData.AgentId, TestContext.Current.CancellationToken)).RequireResolved();
        var identity = CompositionTestData.Identity();
        var started = await agent.StreamAsync<string>(
            CompositionTestData.SessionId,
            identity,
            CompositionTestData.Input(),
            options: CompositionTestData.RunOptions(),
            cancellationToken: TestContext.Current.CancellationToken);
        var stream = started.ShouldBeOfType<AgentRunStreamStarted<string>>().Stream;
        await loop.Entered.Task;
        var runId = loop.Requests.Single().RunId;
        var services = loop.Services.Single();

        var attached = (await agent.AttachAsync<string>(runId, identity, TestContext.Current.CancellationToken))
            .ShouldBeOfType<AgentRunStreamStarted<string>>().Stream;

        if (services.Publisher is IOutputPublisher publisher)
        {
            await publisher.PublishAsync(
                new ContentDeltaEvent(
                    CompositionTestData.AgentId,
                    CompositionTestData.SessionId,
                    null,
                    runId,
                    loop.Requests.Single().LaneAdmission!.AcceptedCorrelation.TurnId,
                    sequence: 1,
                    DateTimeOffset.UnixEpoch,
                    new ModelRequestId(Guid.NewGuid()),
                    0,
                    new TextContentDelta("live")),
                TestContext.Current.CancellationToken);
        }

        loop.Gate!.SetResult();
        var replayEvents = new List<RunEvent>();
        await foreach (var runEvent in attached.ReadAllAsync(TestContext.Current.CancellationToken))
        {
            replayEvents.Add(runEvent);
        }

        replayEvents.ShouldContain(static e => e is MessageCommittedEvent);
        replayEvents.ShouldContain(static e => e is ContentDeltaEvent);
        _ = await attached.Completion;
        _ = await stream.Completion;
    }

    private static AgentEngine BuildStreamableEngine(GatedAgentLoop loop, InMemoryTestSessionCoordinator sessions)
    {
        var definition = CompositionTestData.Definition() with
        {
            InputCoordinatorKey = InputKey,
            OutputPublisherKey = OutputKey,
        };
        var builder = CompositionTestData.SendableBuilder(loop, sessions, definition: definition);
        _ = builder.Services.AddAgentIO(InputKey, OutputKey);
        return builder.Build();
    }
}
