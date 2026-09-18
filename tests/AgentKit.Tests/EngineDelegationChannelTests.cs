// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

using System.Collections.Immutable;

using AgentKit.TestSupport;

using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

/// <summary>Verifies <see cref="EngineDelegationChannel"/> runs a delegated task as one turn of the target agent on the same engine.</summary>
public sealed class EngineDelegationChannelTests
{
    private static readonly AgentId _specialist = new(Guid.Parse("a0000000-0000-0000-0000-00000000000c"));

    [Fact]
    public void Constructor_WhenAnArgumentIsNull_ThrowsArgumentNullException()
    {
        var options = Options.Create(new EngineDelegationChannelOptions());
        var goalIds = new CountingGoalIdGenerator();
        using var provider = new ServiceCollection().BuildServiceProvider();

        Should.Throw<ArgumentNullException>(() => new EngineDelegationChannel(null!, TimeProvider.System, goalIds, options)).ParamName.ShouldBe("services");
        Should.Throw<ArgumentNullException>(() => new EngineDelegationChannel(provider, null!, goalIds, options)).ParamName.ShouldBe("timeProvider");
        Should.Throw<ArgumentNullException>(() => new EngineDelegationChannel(provider, TimeProvider.System, null!, options)).ParamName.ShouldBe("goalIds");
        Should.Throw<ArgumentNullException>(() => new EngineDelegationChannel(provider, TimeProvider.System, goalIds, null!)).ParamName.ShouldBe("options");
    }

    [Fact]
    public void Constructor_WhenTheSummaryBoundIsNotPositive_ThrowsArgumentOutOfRangeException()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();

        Should.Throw<ArgumentOutOfRangeException>(() => new EngineDelegationChannel(
            provider, TimeProvider.System, new CountingGoalIdGenerator(), Options.Create(new EngineDelegationChannelOptions { MaximumSummaryCharacters = 0 })))
            .ParamName.ShouldBe("options");
    }

    [Fact]
    public async Task DelegateAsync_WhenPromptIsNull_ThrowsArgumentNullException()
    {
        var (engine, _, _) = await BuildAsync(new GatedAgentLoop());
        await using var owned = engine;
        var channel = engine.Services.GetRequiredService<ITaskDelegationChannel>();

        var exception = await Should.ThrowAsync<ArgumentNullException>(async () => await channel.DelegateAsync(null!, TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("prompt");
    }

    [Fact]
    public async Task DelegateAsync_WhenTheTargetIsHosted_RunsOneTurnInANewSessionAndReturnsItsIdentitiesAndSummary()
    {
        var loop = new GatedAgentLoop();
        var (engine, sessions, _) = await BuildAsync(loop);
        await using var owned = engine;
        var channel = engine.Services.GetRequiredService<ITaskDelegationChannel>();
        var prompt = Prompt(_specialist, maximumTurns: 3, criteria: ["Cite files."]);

        var result = await channel.DelegateAsync(prompt, TestContext.Current.CancellationToken);

        var child = result.ShouldBeOfType<TaskDelegationChildResult>();
        child.Id.ShouldBe(prompt.Id);
        child.ChildAgentId.ShouldBe(_specialist);
        child.Status.ShouldBe(TaskDelegationStatus.Succeeded);
        child.Summary.ShouldBe("ok");
        child.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyNotPerformed);
        var request = loop.Requests.ShouldHaveSingleItem();
        request.AgentId.ShouldBe(_specialist);
        request.Identity.ShouldBe(prompt.Identity);
        request.MaxTurns.ShouldBe(3);
        child.ChildSessionId.ShouldBe(request.SessionId);
        child.ChildRunId.ShouldBe(request.RunId);
        sessions.CreateRequests.ShouldHaveSingleItem().AgentId.ShouldBe(_specialist);
        var user = sessions.EntriesOf(request.SessionId).OfType<MessageSessionEntry>().Single().Message.ShouldBeOfType<UserMessage>();
        var text = user.Parts.OfType<TextPart>().Single().Text;
        text.ShouldContain("Find the retry policy.");
        text.ShouldContain("- Cite files.");
    }

    [Fact]
    public async Task DelegateAsync_WhenTheBudgetExceedsTheTargetsDefault_NarrowsToTheDefinition()
    {
        var loop = new GatedAgentLoop();
        var (engine, _, _) = await BuildAsync(loop);
        await using var owned = engine;
        var channel = engine.Services.GetRequiredService<ITaskDelegationChannel>();

        _ = await channel.DelegateAsync(Prompt(_specialist, maximumTurns: 50), TestContext.Current.CancellationToken);

        loop.Requests.Single().MaxTurns.ShouldBe(4);
    }

    [Fact]
    public async Task DelegateAsync_WhenTheTargetIsNotHosted_RejectsWithoutRunningAnything()
    {
        var loop = new GatedAgentLoop();
        var (engine, sessions, _) = await BuildAsync(loop);
        await using var owned = engine;
        var channel = engine.Services.GetRequiredService<ITaskDelegationChannel>();

        var result = await channel.DelegateAsync(Prompt(new AgentId(Guid.NewGuid())), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<TaskDelegationRejected>().SafeMessage.ShouldContain("not hosted");
        loop.Requests.ShouldBeEmpty();
        sessions.CreateRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task DelegateAsync_WhenTheDeadlineHasPassed_RejectsWithoutRunningAnything()
    {
        var loop = new GatedAgentLoop();
        var (engine, _, clock) = await BuildAsync(loop);
        await using var owned = engine;
        var channel = engine.Services.GetRequiredService<ITaskDelegationChannel>();

        var result = await channel.DelegateAsync(Prompt(_specialist, deadline: clock.GetUtcNow().AddSeconds(-1)), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<TaskDelegationRejected>().SafeMessage.ShouldContain("deadline");
        loop.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task DelegateAsync_WhenTheDeadlineElapsesMidRun_RejectsNamingTheDeadline()
    {
        var loop = new GatedAgentLoop { Gate = new TaskCompletionSource() };
        var (engine, _, clock) = await BuildAsync(loop);
        await using var owned = engine;
        var channel = engine.Services.GetRequiredService<ITaskDelegationChannel>();

        var pending = channel.DelegateAsync(Prompt(_specialist, deadline: clock.GetUtcNow().AddMinutes(1)), TestContext.Current.CancellationToken).AsTask();
        await loop.Entered.Task;
        clock.Advance(TimeSpan.FromMinutes(2));
        var result = await pending;

        result.ShouldBeOfType<TaskDelegationRejected>().SafeMessage.ShouldContain("deadline elapsed");
    }

    [Fact]
    public async Task DelegateAsync_WhenTheChildSettlesCancelled_ReportsCancelledWithItsIdentities()
    {
        var loop = new GatedAgentLoop { OutcomeOverride = request => new AgentRunCancelled("stopped") };
        var (engine, _, _) = await BuildAsync(loop);
        await using var owned = engine;
        var channel = engine.Services.GetRequiredService<ITaskDelegationChannel>();

        var result = await channel.DelegateAsync(Prompt(_specialist), TestContext.Current.CancellationToken);

        var child = result.ShouldBeOfType<TaskDelegationChildResult>();
        child.Status.ShouldBe(TaskDelegationStatus.Cancelled);
        child.ChildSessionId.ShouldBe(loop.Requests.Single().SessionId);
        child.Summary.ShouldContain("AgentRunCancelled");
    }

    [Fact]
    public async Task DelegateAsync_WhenTheCallerCancels_Propagates()
    {
        var loop = new GatedAgentLoop { Gate = new TaskCompletionSource() };
        var (engine, _, _) = await BuildAsync(loop);
        await using var owned = engine;
        var channel = engine.Services.GetRequiredService<ITaskDelegationChannel>();
        using var cancellation = new CancellationTokenSource();

        var pending = channel.DelegateAsync(Prompt(_specialist), cancellation.Token).AsTask();
        await loop.Entered.Task;
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await pending);
    }

    [Fact]
    public async Task DelegateAsync_WhenTheSummaryIsLong_TruncatesToTheConfiguredBound()
    {
        var loop = new GatedAgentLoop();
        var (engine, _, _) = await BuildAsync(loop, configure: o => o.MaximumSummaryCharacters = 1);
        await using var owned = engine;
        var channel = engine.Services.GetRequiredService<ITaskDelegationChannel>();

        var result = await channel.DelegateAsync(Prompt(_specialist), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<TaskDelegationChildResult>().Summary.ShouldBe("o");
    }

    [Fact]
    public void AddEngineDelegationChannel_WhenCalledTwice_RegistersOnce()
    {
        var services = new ServiceCollection();

        _ = services.AddEngineDelegationChannel().AddEngineDelegationChannel(o => o.MaximumSummaryCharacters = 5);

        services.Count(static d => d.ServiceType == typeof(ITaskDelegationChannel)).ShouldBe(1);
        services.Count(static d => d.ServiceType == typeof(IIdentifierGenerator<GoalId>)).ShouldBe(1);
    }

    [Fact]
    public void AddEngineDelegationChannel_WhenServicesIsNull_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;

        Should.Throw<ArgumentNullException>(() => services.AddEngineDelegationChannel()).ParamName.ShouldBe("services");
    }

    private static async Task<(AgentEngine Engine, InMemoryTestSessionCoordinator Sessions, FakeTimeProvider Clock)> BuildAsync(
        GatedAgentLoop loop,
        Action<EngineDelegationChannelOptions>? configure = null)
    {
        var sessions = new InMemoryTestSessionCoordinator();
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 9, 18, 12, 0, 0, TimeSpan.Zero));
        var builder = CompositionTestData.SendableBuilder(
            loop, sessions, SessionBusyBehavior.Reject,
            CompositionTestData.Definition(), CompositionTestData.Definition(_specialist, "specialist", maxTurns: 4));
        _ = builder.Services.ReplaceTimeProvider(clock);
        _ = builder.Services.AddEngineDelegationChannel(configure);
        var engine = builder.Build();
        _ = await engine.GetAgentsAsync(TestContext.Current.CancellationToken);
        return (engine, sessions, clock);
    }

    private static TaskDelegationPrompt Prompt(
        AgentId target,
        int maximumTurns = 2,
        ImmutableArray<string> criteria = default,
        DateTimeOffset? deadline = null)
    {
        var parentRunId = new RunId(Guid.NewGuid());
        return new(
            new DelegationId(Guid.NewGuid()),
            CompositionTestData.AgentId,
            CompositionTestData.SessionId,
            parentRunId,
            new InRunOperationCorrelation(new OperationId(Guid.NewGuid()), parentRunId, null),
            new ToolCallId(Guid.NewGuid()),
            CompositionTestData.Identity(),
            target,
            "Find the retry policy.",
            criteria.IsDefault ? ["Answer briefly."] : criteria,
            [],
            new TaskDelegationBudget(maximumTurns, 10),
            deadline ?? new DateTimeOffset(2026, 9, 18, 13, 0, 0, TimeSpan.Zero));
    }

    private sealed class CountingGoalIdGenerator: IIdentifierGenerator<GoalId>
    {
        public GoalId Create() => new(Guid.NewGuid());
    }
}
