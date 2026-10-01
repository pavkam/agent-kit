// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Hosting.Tests;

using AgentKit.Tests;

public sealed class EngineDelegationChildRunnerTests
{
    private static readonly AgentId _specialist = new(Guid.Parse("a0000000-0000-0000-0000-00000000000c"));

    private static async Task<(AgentEngine Engine, InMemoryTestSessionCoordinator Sessions, IDelegationChildRunner Runner)> BuildAsync(
        GatedAgentLoop loop,
        Action<GoalWorkerOptions>? configure = null)
    {
        var sessions = new InMemoryTestSessionCoordinator();
        var builder = CompositionTestData.SendableBuilder(
            loop, sessions, SessionBusyBehavior.Reject,
            CompositionTestData.Definition(), CompositionTestData.Definition(_specialist, "specialist", maxTurns: 4));
        _ = builder.Services.AddLogging();
        CompositionTestData.UseFirstPartyIo(builder.Services);
        _ = builder.Services.AddOptions<GoalWorkerOptions>();
        if (configure is not null)
        {
            _ = builder.Services.Configure(configure);
        }

        _ = builder.Services.AddSingleton<IDelegationChildRunner, EngineDelegationChildRunner>();
        var engine = builder.Build();
        _ = await engine.GetAgentsAsync(TestContext.Current.CancellationToken);
        return (engine, sessions, engine.Services.GetRequiredService<IDelegationChildRunner>());
    }

    private static DelegationRequest Delegation(AgentId target, int maximumTurns = 2, ImmutableArray<string> criteria = default)
    {
        var agent = CompositionTestData.AgentId;
        var session = CompositionTestData.SessionId;
        var run = GoalTestData.NewRun();
        var parent = GoalTestData.Goal(agent, session, run, id: RunRootGoal.GoalIdFor(run));
        var authorization = GoalTestData.Authorization(agent, session, run, CompositionTestData.Identity());
        var request = GoalTestData.Delegation(parent, RunRootGoal.AttemptIdFor(run), run, target, "k", authorization, new GoalBudget(maximumTurns, 10, 0));
        return new DelegationRequest(
            request.Id, request.ParentGoalId, request.ParentAttemptId, request.ParentAgentId, request.ParentSessionId, request.ParentRunId,
            request.ProfileKey, request.ProfileVersion, request.AgentDefinitionRevision, request.Authorization, request.OperationId, request.TargetAgentId,
            new GoalDefinition("Find the retry policy.", [], ExtensionData.Empty),
            new AcceptanceCriteria(criteria.IsDefault ? ["Answer briefly."] : criteria, requiresEvidence: false),
            request.Scope, request.Budget, request.Deadline, request.CancellationMode, request.JoinStrategyKey, request.IdempotencyKey);
    }

    private static DelegationChildRunRequest Run(DelegationRequest delegation, SessionId session) =>
        new(delegation, new GoalId(Guid.NewGuid()), session, new GoalAttemptId(Guid.NewGuid()));

    [Fact]
    public void Constructor_WhenAnArgumentIsNull_ThrowsArgumentNullException()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var options = Options.Create(new GoalWorkerOptions());

        Should.Throw<ArgumentNullException>(() => new EngineDelegationChildRunner(null!, options)).ParamName.ShouldBe("services");
        Should.Throw<ArgumentNullException>(() => new EngineDelegationChildRunner(provider, null!)).ParamName.ShouldBe("options");
    }

    [Fact]
    public async Task ProvisionSessionAsync_WhenArgumentsAreInvalid_ThrowsWithParameterName()
    {
        var (engine, _, runner) = await BuildAsync(new GatedAgentLoop());
        await using var owned = engine;
        var delegation = Delegation(_specialist);

        (await Should.ThrowAsync<ArgumentNullException>(async () => await runner.ProvisionSessionAsync(null!, new GoalId(Guid.NewGuid()), 1, TestContext.Current.CancellationToken))).ParamName.ShouldBe("delegation");
        (await Should.ThrowAsync<ArgumentOutOfRangeException>(async () => await runner.ProvisionSessionAsync(delegation, default, 1, TestContext.Current.CancellationToken))).ParamName.ShouldBe("childGoalId");
        (await Should.ThrowAsync<ArgumentOutOfRangeException>(async () => await runner.ProvisionSessionAsync(delegation, new GoalId(Guid.NewGuid()), 0, TestContext.Current.CancellationToken))).ParamName.ShouldBe("attemptNumber");
    }

    [Fact]
    public async Task RunAsync_WhenRequestIsNull_ThrowsArgumentNullException()
    {
        var (engine, _, runner) = await BuildAsync(new GatedAgentLoop());
        await using var owned = engine;

        (await Should.ThrowAsync<ArgumentNullException>(async () => await runner.RunAsync(null!, TestContext.Current.CancellationToken))).ParamName.ShouldBe("request");
    }

    [Fact]
    public async Task ProvisionSessionAsync_WhenTargetIsHosted_CreatesOneSessionPerGoalAndAttempt()
    {
        var (engine, sessions, runner) = await BuildAsync(new GatedAgentLoop());
        await using var owned = engine;
        var delegation = Delegation(_specialist);
        var goal = new GoalId(Guid.NewGuid());

        var first = await runner.ProvisionSessionAsync(delegation, goal, 1, TestContext.Current.CancellationToken);
        var again = await runner.ProvisionSessionAsync(delegation, goal, 1, TestContext.Current.CancellationToken);
        var next = await runner.ProvisionSessionAsync(delegation, goal, 2, TestContext.Current.CancellationToken);

        _ = first.ShouldNotBeNull();
        _ = again.ShouldNotBeNull();
        _ = next.ShouldNotBeNull();
        sessions.CreateRequests.ShouldAllBe(static request => request.AgentId == _specialist);
        sessions.CreateRequests.Select(static request => request.IdempotencyKey).Distinct().Count().ShouldBe(2);
        sessions.CreateRequests[0].IdempotencyKey.ShouldBe(sessions.CreateRequests[1].IdempotencyKey);
    }

    [Fact]
    public async Task ProvisionSessionAsync_WhenTargetIsNotHosted_ReturnsNull()
    {
        var (engine, sessions, runner) = await BuildAsync(new GatedAgentLoop());
        await using var owned = engine;

        var session = await runner.ProvisionSessionAsync(Delegation(new AgentId(Guid.NewGuid())), new GoalId(Guid.NewGuid()), 1, TestContext.Current.CancellationToken);

        session.ShouldBeNull();
        sessions.CreateRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task RunAsync_WhenTargetIsHosted_RunsOneTurnInTheProvisionedSessionAndReportsRunAndSummary()
    {
        var loop = new GatedAgentLoop();
        var (engine, sessions, runner) = await BuildAsync(loop);
        await using var owned = engine;
        var delegation = Delegation(_specialist, maximumTurns: 3, criteria: ["Cite files."]);
        var session = (await runner.ProvisionSessionAsync(delegation, new GoalId(Guid.NewGuid()), 1, TestContext.Current.CancellationToken)).ShouldNotBeNull();

        var result = await runner.RunAsync(Run(delegation, session), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(DelegationStatus.Succeeded);
        result.Summary.ShouldBe("ok");
        result.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyNotPerformed);
        var request = loop.Requests.ShouldHaveSingleItem();
        request.AgentId.ShouldBe(_specialist);
        request.SessionId.ShouldBe(session);
        request.Identity.ShouldBe(delegation.Authorization.Identity);
        request.MaxTurns.ShouldBe(3);
        result.RunId.ShouldBe(request.RunId);
        var text = sessions.EntriesOf(request.SessionId).OfType<MessageSessionEntry>().Single().Message.ShouldBeOfType<UserMessage>().Parts.OfType<TextPart>().Single().Text;
        text.ShouldContain("Find the retry policy.");
        text.ShouldContain("- Cite files.");
    }

    [Fact]
    public async Task RunAsync_WhenTheBudgetExceedsTheTargetsDefault_NarrowsToTheDefinition()
    {
        var loop = new GatedAgentLoop();
        var (engine, _, runner) = await BuildAsync(loop);
        await using var owned = engine;
        var delegation = Delegation(_specialist, maximumTurns: 50);
        var session = (await runner.ProvisionSessionAsync(delegation, new GoalId(Guid.NewGuid()), 1, TestContext.Current.CancellationToken)).ShouldNotBeNull();

        _ = await runner.RunAsync(Run(delegation, session), TestContext.Current.CancellationToken);

        loop.Requests.Single().MaxTurns.ShouldBe(4);
    }

    [Fact]
    public async Task RunAsync_WhenTheTargetIsNotHosted_ReturnsAFailedResultThatPerformedNothing()
    {
        var loop = new GatedAgentLoop();
        var (engine, _, runner) = await BuildAsync(loop);
        await using var owned = engine;

        var result = await runner.RunAsync(Run(Delegation(new AgentId(Guid.NewGuid())), new SessionId(Guid.NewGuid())), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(DelegationStatus.Failed);
        result.RunId.ShouldBeNull();
        result.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyNotPerformed);
        loop.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task RunAsync_WhenTheChildSettlesCancelled_ReportsCancelledWithItsRun()
    {
        var loop = new GatedAgentLoop { OutcomeOverride = static _ => new RunCancelled(new(RunResultTestData.Error(AgentErrorCodes.Cancelled))) };
        var (engine, _, runner) = await BuildAsync(loop);
        await using var owned = engine;
        var delegation = Delegation(_specialist);
        var session = (await runner.ProvisionSessionAsync(delegation, new GoalId(Guid.NewGuid()), 1, TestContext.Current.CancellationToken)).ShouldNotBeNull();

        var result = await runner.RunAsync(Run(delegation, session), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(DelegationStatus.Cancelled);
        result.Summary.ShouldBeNull();
        result.RunId.ShouldBe(loop.Requests.Single().RunId);
    }

    [Fact]
    public async Task RunAsync_WhenTheSummaryIsLong_TruncatesToTheConfiguredBound()
    {
        var (engine, _, runner) = await BuildAsync(new GatedAgentLoop(), static options => options.MaximumSummaryCharacters = 1);
        await using var owned = engine;
        var delegation = Delegation(_specialist);
        var session = (await runner.ProvisionSessionAsync(delegation, new GoalId(Guid.NewGuid()), 1, TestContext.Current.CancellationToken)).ShouldNotBeNull();

        var result = await runner.RunAsync(Run(delegation, session), TestContext.Current.CancellationToken);

        result.Summary.ShouldBe("o");
    }

    [Fact]
    public async Task RunAsync_WhenTheCallerCancels_StopsWaitingAndPropagatesCancellation()
    {
        var loop = new GatedAgentLoop { Gate = new TaskCompletionSource() };
        var (engine, _, runner) = await BuildAsync(loop);
        await using var owned = engine;
        var delegation = Delegation(_specialist);
        var session = (await runner.ProvisionSessionAsync(delegation, new GoalId(Guid.NewGuid()), 1, TestContext.Current.CancellationToken)).ShouldNotBeNull();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var pending = runner.RunAsync(Run(delegation, session), cancellation.Token).AsTask();
        await loop.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
    }
}
