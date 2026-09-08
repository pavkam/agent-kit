// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

public sealed class AgentEngineTests
{
    [Fact]
    public void Constructor_WhenServicesIsNull_ThrowsBeforeAssignment()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new AgentEngine(
                null!, ownedProvider: null, new AgentRunProfilePublicationSnapshot([])));

        exception.ParamName.ShouldBe("services");
    }

    [Fact]
    public void Constructor_WhenValidatedRunProfilesIsNull_ThrowsBeforeResolvingServices()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new AgentEngine(MinimalProvider(), ownedProvider: null, null!));

        exception.ParamName.ShouldBe("validatedRunProfiles");
    }

    [Fact]
    public async Task GetAgentAsync_WhenAgentIsPublished_ReturnsHandleOverItsDefinition()
    {
        await using var engine = CompositionTestData.RunnableBuilder().Build();

        var agent = await engine.GetAgentAsync(
            CompositionTestData.AgentId,
            TestContext.Current.CancellationToken);

        _ = agent.ShouldNotBeNull();
        agent.Id.ShouldBe(CompositionTestData.AgentId);
        agent.Definition.DisplayName.ShouldBe("test agent");
        agent.CatalogVersion.Value.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task GetAgentAsync_WhenAgentIsUnknown_ReturnsNullRatherThanThrowing()
    {
        await using var engine = CompositionTestData.RunnableBuilder().Build();

        var agent = await engine.GetAgentAsync(
            new AgentId(Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        agent.ShouldBeNull();
    }

    [Fact]
    public async Task GetAgentsAsync_ReturnsEveryPublishedDefinition()
    {
        var second = CompositionTestData.Definition(new AgentId(Guid.NewGuid()), "second");
        var builder = CompositionTestData.RunnableBuilder();
        _ = builder.Services.AddAgent(second);
        _ = builder.Services.AddAgentRunProfilePublication(CompositionTestData.RunProfile(second));

        await using var engine = builder.Build();

        var agents = await engine.GetAgentsAsync(TestContext.Current.CancellationToken);

        agents.Length.ShouldBe(2);
    }

    [Fact]
    public async Task RunAsync_BuildsARequestFromTheDefinitionAndOptions()
    {
        var loop = new RecordingAgentLoop();
        await using var engine = CompositionTestData.RunnableBuilder(loop).Build();
        var agent = (await engine.GetAgentAsync(
            CompositionTestData.AgentId,
            TestContext.Current.CancellationToken))!;

        var result = await agent.RunAsync(
            CompositionTestData.RunOptions(),
            TestContext.Current.CancellationToken);

        var request = loop.ReceivedRequests.ShouldHaveSingleItem();
        request.AgentId.ShouldBe(CompositionTestData.AgentId);
        request.SessionId.ShouldBe(CompositionTestData.SessionId);
        request.BranchId.ShouldBe(CompositionTestData.BranchId);
        request.MaxTurns.ShouldBe(8);
        request.ModelPolicy.Candidates.ShouldHaveSingleItem().Value.ShouldBe("chat");
        result.RunId.ShouldBe(request.RunId);
    }

    [Fact]
    public async Task RunAsync_WhenCalledTwice_AllocatesADistinctRunIdEachTime()
    {
        var loop = new RecordingAgentLoop();
        await using var engine = CompositionTestData.RunnableBuilder(loop).Build();
        var agent = (await engine.GetAgentAsync(
            CompositionTestData.AgentId,
            TestContext.Current.CancellationToken))!;

        _ = await agent.RunAsync(CompositionTestData.RunOptions(), TestContext.Current.CancellationToken);
        _ = await agent.RunAsync(CompositionTestData.RunOptions(), TestContext.Current.CancellationToken);

        loop.ReceivedRequests[0].RunId.ShouldNotBe(loop.ReceivedRequests[1].RunId);
    }

    [Fact]
    public async Task RunAsync_WhenOverrideNarrowsTurnLimit_UsesTheNarrowerValue()
    {
        var loop = new RecordingAgentLoop();
        await using var engine = CompositionTestData.RunnableBuilder(loop).Build();
        var agent = (await engine.GetAgentAsync(
            CompositionTestData.AgentId,
            TestContext.Current.CancellationToken))!;

        _ = await agent.RunAsync(
            CompositionTestData.RunOptions(maxTurns: 3),
            TestContext.Current.CancellationToken);

        loop.ReceivedRequests.ShouldHaveSingleItem().MaxTurns.ShouldBe(3);
    }

    [Fact]
    public async Task RunAsync_WhenOverrideWidensTurnLimit_ThrowsBeforeRunning()
    {
        var loop = new RecordingAgentLoop();
        await using var engine = CompositionTestData.RunnableBuilder(loop).Build();
        var agent = (await engine.GetAgentAsync(
            CompositionTestData.AgentId,
            TestContext.Current.CancellationToken))!;

        var exception = await Should.ThrowAsync<ArgumentOutOfRangeException>(
            async () => await agent.RunAsync(
                CompositionTestData.RunOptions(maxTurns: 99),
                TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("options");
        loop.ReceivedRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task RunAsync_WhenOverrideWidensAttemptTimeout_ThrowsBeforeRunning()
    {
        var loop = new RecordingAgentLoop();
        await using var engine = CompositionTestData.RunnableBuilder(loop).Build();
        var agent = (await engine.GetAgentAsync(
            CompositionTestData.AgentId,
            TestContext.Current.CancellationToken))!;

        _ = await Should.ThrowAsync<ArgumentOutOfRangeException>(
            async () => await agent.RunAsync(
                CompositionTestData.RunOptions(attemptTimeout: TimeSpan.FromHours(1)),
                TestContext.Current.CancellationToken));

        loop.ReceivedRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task RunAsync_WhenOptionsIsNull_ThrowsArgumentNullException()
    {
        await using var engine = CompositionTestData.RunnableBuilder().Build();
        var agent = (await engine.GetAgentAsync(
            CompositionTestData.AgentId,
            TestContext.Current.CancellationToken))!;

        var exception = await Should.ThrowAsync<ArgumentNullException>(
            async () => await agent.RunAsync(null!, TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("options");
    }

    [Fact]
    public async Task DisposeAsync_WhenCalledConcurrently_DisposesOwnerOnceAndSharesCompletion()
    {
        var owner = new BlockingAsyncDisposable();
        var engine = new AgentEngine(
            MinimalProvider(), owner, new AgentRunProfilePublicationSnapshot([]));

        var firstDisposal = engine.DisposeAsync().AsTask();
        var secondDisposal = engine.DisposeAsync().AsTask();

        firstDisposal.ShouldBeSameAs(secondDisposal);
        owner.DisposeCount.ShouldBe(1);
        firstDisposal.IsCompleted.ShouldBeFalse();

        owner.Complete();
        await Task.WhenAll(firstDisposal, secondDisposal);

        owner.DisposeCount.ShouldBe(1);
    }

    [Fact]
    public async Task DisposeAsync_WhenOwnerThrowsSynchronously_CachesFailureWithoutRetrying()
    {
        var owner = new ThrowingAsyncDisposable();
        var engine = new AgentEngine(
            MinimalProvider(), owner, new AgentRunProfilePublicationSnapshot([]));

        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await engine.DisposeAsync());
        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await engine.DisposeAsync());

        owner.DisposeCount.ShouldBe(1);
    }

    private static ServiceProvider MinimalProvider()
    {
        var services = new ServiceCollection();
        _ = services.AddAgentKit();
        _ = services.AddSingleton<ISecurityProfileSelector>(new TestSecurityProfileSelector());
        return services.BuildServiceProvider();
    }

    private sealed class BlockingAsyncDisposable: IAsyncDisposable
    {
        private readonly TaskCompletionSource _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int DisposeCount { get; private set; }

        public void Complete() => _completion.SetResult();

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return new ValueTask(_completion.Task);
        }
    }

    private sealed class ThrowingAsyncDisposable: IAsyncDisposable
    {
        public int DisposeCount { get; private set; }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            throw new InvalidOperationException("disposal failed");
        }
    }
}
