// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

using AgentKit.TestSupport;

using Microsoft.Extensions.Logging;

public sealed class AgentEngineTests
{
    [Fact]
    public async Task UnsupportedSessionCoordinator_WhenInvokedDirectly_ThrowsFromEveryMemberBecauseCompositionFakesMustNeverBeCalled()
    {
        var coordinator = new UnsupportedSessionCoordinator();

        _ = await Should.ThrowAsync<NotSupportedException>(
            async () => await coordinator.CreateAsync(null!, null!, TestContext.Current.CancellationToken));
        _ = await Should.ThrowAsync<NotSupportedException>(
            async () => await coordinator.LoadAsync(null!, null!, TestContext.Current.CancellationToken));
        _ = await Should.ThrowAsync<NotSupportedException>(
            async () => await coordinator.AppendAsync(null!, null!, TestContext.Current.CancellationToken));
        _ = await Should.ThrowAsync<NotSupportedException>(
            async () => await coordinator.ReadAsync(null!, null!, TestContext.Current.CancellationToken));
        _ = await Should.ThrowAsync<NotSupportedException>(
            async () => await coordinator.BranchAsync(null!, null!, TestContext.Current.CancellationToken));
        _ = await Should.ThrowAsync<NotSupportedException>(
            async () => await coordinator.DeleteAsync(null!, null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Constructor_WhenServicesIsNull_ThrowsBeforeAssignment()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new AgentEngineRuntime(
                null!, ownedProvider: null, Composition()));

        exception.ParamName.ShouldBe("services");
    }

    [Fact]
    public void Constructor_WhenValidatedCompositionIsNull_ThrowsBeforeResolvingServices()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new AgentEngineRuntime(MinimalProvider(), ownedProvider: null, null!));

        exception.ParamName.ShouldBe("validatedComposition");
    }

    [Fact]
    public async Task GetAgentAsync_WhenAgentIsPublished_ReturnsHandleOverItsDefinition()
    {
        await using var engine = CompositionTestData.RunnableBuilder().Build();

        var resolution = await engine.GetAgentAsync(
            CompositionTestData.AgentId,
            TestContext.Current.CancellationToken);

        var agent = resolution.ShouldBeOfType<ResolvedAgent>().Agent;
        agent.Id.ShouldBe(CompositionTestData.AgentId);
        agent.Definition.DisplayName.ShouldBe("test agent");
        agent.CatalogVersion.Value.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task GetAgentAsync_WhenAgentIsUnknown_ReturnsAgentNotFoundRatherThanThrowing()
    {
        await using var engine = CompositionTestData.RunnableBuilder().Build();
        var agentId = new AgentId(Guid.NewGuid());

        var resolution = await engine.GetAgentAsync(
            agentId,
            TestContext.Current.CancellationToken);

        resolution.ShouldBeOfType<AgentNotFound>().AgentId.ShouldBe(agentId);
    }

    [Fact]
    public async Task GetAgentAsync_WhenCatalogReportsAnInvalidDefinition_ReturnsInvalidAgentWithDiagnostics()
    {
        var agentId = new AgentId(Guid.NewGuid());
        var diagnostic = new CompositionDiagnostic("agentkit.test.invalid", "The definition selects a missing component.");
        var resolution = new InvalidAgentDefinition(agentId, [diagnostic]);
        var services = new ServiceCollection();
        _ = services.AddAgentKit();
        _ = services.AddSingleton<ISecurityProfileSelector>(new TestSecurityProfileSelector());
        _ = services.AddSingleton<IAgentDefinitionCatalog>(new FixedResolutionAgentDefinitionCatalog(resolution));
        await using var provider = services.BuildServiceProvider();
        await using var engine = new AgentEngine(new AgentEngineRuntime(provider, ownedProvider: null, Composition()));

        var result = await engine.GetAgentAsync(agentId, TestContext.Current.CancellationToken);

        var invalid = result.ShouldBeOfType<InvalidAgent>();
        invalid.AgentId.ShouldBe(agentId);
        invalid.Diagnostics.ShouldHaveSingleItem().ShouldBe(diagnostic);
    }

    [Fact]
    public async Task RunAsync_WhenNoRunProfileIsPinnedForTheDefinition_RejectsWithoutCreatingARunOrScope()
    {
        var definition = CompositionTestData.Definition(new AgentId(Guid.NewGuid()));
        var catalog = new MutableAgentDefinitionCatalog(definition);
        var runIds = new CountingRunIdGenerator();
        var builder = AgentEngine.CreateBuilder();
        _ = builder.Services.Replace(ServiceDescriptor.Singleton<IAgentDefinitionCatalog>(catalog));
        _ = builder.Services.Replace(ServiceDescriptor.Singleton<IIdentifierGenerator<RunId>>(runIds));
        _ = builder.Services.AddKeyedScoped<IAgentLoop>(
            AgentLoopComponentDefaults.LoopKeyValue, (_, _) => new ScopedRecordingAgentLoop(new AdmissionRunEffects()));
        CompositionTestData.AddRunServicesFakes(builder.Services);
        CompositionTestData.AddRunProfiles(builder.Services, definition);
        await using var successfullyBuilt = builder.Build();
        await using var provider = builder.Services.BuildServiceProvider();
        await using var engine = new AgentEngine(new AgentEngineRuntime(
            provider,
            ownedProvider: null,
            new AgentCompositionSnapshot(
                new AgentRunProfilePublicationSnapshot([]),
                successfullyBuilt.ComponentRegistrations)));
        var agent = (await engine.GetAgentAsync(definition.Id, TestContext.Current.CancellationToken)).RequireResolved();

        var rejected = (await agent.RunAsync<string>(CompositionTestData.SessionId, CompositionTestData.Identity(), CompositionTestData.Input(), options: CompositionTestData.RunOptions(), cancellationToken: TestContext.Current.CancellationToken)).ShouldBeOfType<AgentRunRejected<string>>();
        rejected.AgentId.ShouldBe(definition.Id);
        rejected.Failure.SafeMessage.ShouldContain("no pinned run-profile publication");
        runIds.Created.ShouldBe(0);
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

        agents.Definitions.Length.ShouldBe(2);
    }

    [Fact]
    public async Task RunAsync_WhenTwoDefinitionsSelectDifferentKeyedCollaborators_EachCompilesItsOwnBundle()
    {
        // Two keyed loops each select a complete, distinct set of keyed collaborators. The run-plan compiler must
        // bind every one by the definition's own key; none may leak from the other definition or an unkeyed default.
        var first = KeyedSet("a");
        var second = KeyedSet("b");
        var firstDefinition = CompositionTestData.Definition(new AgentId(Guid.NewGuid()), "first").WithComponents(
            first.LoopKey, first.PolicyKey, first.InputKey, first.OutputKey, first.ProcessorKey, first.ContextKey, first.SelectorKey, first.ExecutorKey);
        var secondDefinition = CompositionTestData.Definition(new AgentId(Guid.NewGuid()), "second").WithComponents(
            second.LoopKey, second.PolicyKey, second.InputKey, second.OutputKey, second.ProcessorKey, second.ContextKey, second.SelectorKey, second.ExecutorKey);

        var builder = AgentEngine.CreateBuilder();
        var sessions = new InMemoryTestSessionCoordinator();
        var firstSession = new SessionId(Guid.NewGuid());
        var secondSession = new SessionId(Guid.NewGuid());
        _ = builder.Services.AddSingleton<ISessionCoordinator>(sessions);
        CompositionTestData.AddRunServicesFakes(builder.Services);
        first.Register(builder.Services);
        second.Register(builder.Services);
        _ = builder.Services.AddAgent(firstDefinition);
        _ = builder.Services.AddAgent(secondDefinition);
        CompositionTestData.AddRunProfiles(builder.Services, firstDefinition, secondDefinition);
        CompositionTestData.SeedSession(sessions, firstDefinition.Id, firstSession);
        CompositionTestData.SeedSession(sessions, secondDefinition.Id, secondSession);

        await using var engine = builder.Build();
        var firstAgent = (await engine.GetAgentAsync(firstDefinition.Id, TestContext.Current.CancellationToken)).RequireResolved();
        var secondAgent = (await engine.GetAgentAsync(secondDefinition.Id, TestContext.Current.CancellationToken)).RequireResolved();

        _ = await firstAgent.RunAsync<string>(
            firstSession, CompositionTestData.Identity(), CompositionTestData.Input(),
            options: CompositionTestData.RunOptions(),
            cancellationToken: TestContext.Current.CancellationToken);
        _ = await secondAgent.RunAsync<string>(
            secondSession, CompositionTestData.Identity(), CompositionTestData.Input(),
            options: CompositionTestData.RunOptions(),
            cancellationToken: TestContext.Current.CancellationToken);

        first.LoopInstance.ReceivedServices.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            services => services.Context.ShouldBeSameAs(first.Context),
            services => services.ModelSelector.ShouldBeSameAs(first.Selector),
            services => services.ModelExecutor.ShouldBeSameAs(first.Executor),
            services => services.Input.ShouldBeSameAs(first.Input),
            services => services.Publisher.ShouldBeSameAs(first.Output),
            services => services.OutputProcessor.ShouldBeSameAs(first.Processor),
            services => services.ContinuationPolicy.ShouldBeSameAs(first.Policy));
        second.LoopInstance.ReceivedServices.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            services => services.Context.ShouldBeSameAs(second.Context),
            services => services.ModelSelector.ShouldBeSameAs(second.Selector),
            services => services.ModelExecutor.ShouldBeSameAs(second.Executor),
            services => services.Input.ShouldBeSameAs(second.Input),
            services => services.Publisher.ShouldBeSameAs(second.Output),
            services => services.OutputProcessor.ShouldBeSameAs(second.Processor),
            services => services.ContinuationPolicy.ShouldBeSameAs(second.Policy));
    }

    private static KeyedCollaborators KeyedSet(string suffix) => new(suffix);

    /// <summary>One complete set of distinct keyed collaborators a definition can select.</summary>
    private sealed class KeyedCollaborators(string suffix)
    {
        public RecordingAgentLoop LoopInstance { get; } = new();

        public UnsupportedContextAssembler Context { get; } = new();

        public ScriptedModelSelector Selector { get; } = new(new InvalidModelPolicy("unused"));

        public UnsupportedModelRequestExecutor Executor { get; } = new();

        public UnsupportedInputCoordinator Input { get; } = new();

        public UnsupportedOutputPublisher Output { get; } = new();

        public UnsupportedOutputProcessor Processor { get; } = new();

        public UnsupportedRunContinuationPolicy Policy { get; } = new();

        public ComponentKey<IAgentLoop> LoopKey { get; } = new($"loop-{suffix}");

        public ComponentKey<IRunContinuationPolicy> PolicyKey { get; } = new($"policy-{suffix}");

        public ComponentKey<IInputCoordinator> InputKey { get; } = new($"input-{suffix}");

        public ComponentKey<IOutputPublisher> OutputKey { get; } = new($"output-{suffix}");

        public ComponentKey<IOutputProcessor> ProcessorKey { get; } = new($"processor-{suffix}");

        public ComponentKey<IContextAssembler> ContextKey { get; } = new($"context-{suffix}");

        public ComponentKey<IModelSelector> SelectorKey { get; } = new($"selector-{suffix}");

        public ComponentKey<IModelRequestExecutor> ExecutorKey { get; } = new($"executor-{suffix}");

        public void Register(IServiceCollection services)
        {
            _ = services.AddKeyedSingleton<IAgentLoop>(LoopKey.Value, LoopInstance);
            _ = services.AddKeyedSingleton<IRunContinuationPolicy>(PolicyKey.Value, Policy);
            _ = services.AddKeyedSingleton<IInputCoordinator>(InputKey.Value, Input);
            _ = services.AddKeyedSingleton<IOutputPublisher>(OutputKey.Value, Output);
            _ = services.AddKeyedSingleton<IOutputProcessor>(ProcessorKey.Value, Processor);
            _ = services.AddKeyedSingleton<IContextAssembler>(ContextKey.Value, Context);
            _ = services.AddKeyedSingleton<IModelSelector>(SelectorKey.Value, Selector);
            _ = services.AddKeyedSingleton<IModelRequestExecutor>(ExecutorKey.Value, Executor);
        }
    }

    [Fact]
    public async Task RunAsync_BuildsARequestFromTheDefinitionAndOptions()
    {
        var loop = new RecordingAgentLoop();
        await using var engine = CompositionTestData.RunnableBuilder(loop).Build();
        var agent = (await engine.GetAgentAsync(
            CompositionTestData.AgentId,
            TestContext.Current.CancellationToken)).RequireResolved();

        var result = await agent.RunAsync<string>(
            CompositionTestData.SessionId, CompositionTestData.Identity(), CompositionTestData.Input(),
            options: CompositionTestData.RunOptions(),
            cancellationToken: TestContext.Current.CancellationToken);

        var request = loop.ReceivedRequests.ShouldHaveSingleItem();
        request.AgentId.ShouldBe(CompositionTestData.AgentId);
        request.SessionId.ShouldBe(CompositionTestData.SessionId);
        request.BranchId.ShouldBe(CompositionTestData.BranchId);
        request.MaxTurns.ShouldBe(8);
        request.ModelPolicy.Candidates.ShouldHaveSingleItem().Value.ShouldBe("chat");
        result.ShouldBeOfType<AgentRunFinished<string>>().RunId.ShouldBe(request.RunId);
    }

    [Fact]
    public async Task RunAsync_WhenAnOutputProcessorIsRegisteredUnderTheSelectedKey_CompilesItIntoTheRunServices()
    {
        var loop = new RecordingAgentLoop();
        var processor = new NullOutputProcessor();
        var builder = CompositionTestData.RunnableBuilder(loop);
        _ = builder.Services.RemoveAllKeyed<IOutputProcessor>(AgentOutputComponentDefaults.ProcessorKeyValue);
        _ = builder.Services.AddKeyedSingleton<IOutputProcessor>(AgentOutputComponentDefaults.ProcessorKeyValue, processor);
        await using var engine = builder.Build();
        var agent = (await engine.GetAgentAsync(CompositionTestData.AgentId, TestContext.Current.CancellationToken)).RequireResolved();

        _ = await agent.RunAsync<string>(CompositionTestData.SessionId, CompositionTestData.Identity(), CompositionTestData.Input(), options: CompositionTestData.RunOptions(), cancellationToken: TestContext.Current.CancellationToken);

        loop.ReceivedServices.ShouldHaveSingleItem().OutputProcessor.ShouldBeSameAs(processor);
    }

    [Fact]
    public async Task RunAsync_WhenTheDefinitionSelectsAnOutput_CarriesItOnTheRequest()
    {
        var loop = new RecordingAgentLoop();
        var output = new OutputDefinition(
            new OutputDefinitionId("answer"), new OutputDefinitionVersion("1"), "answer", OutputMode.Text,
            schema: null, runtimeType: null, alternatives: [], validators: [],
            OutputValidationPolicy.RejectOnFirstFailure, OutputRetryPolicy.None, OutputEndStrategy.Graceful);
        var definition = CompositionTestData.Definition() with { Output = output };
        await using var engine = CompositionTestData.RunnableBuilder(loop, definition).Build();
        var agent = (await engine.GetAgentAsync(CompositionTestData.AgentId, TestContext.Current.CancellationToken)).RequireResolved();

        _ = await agent.RunAsync<string>(CompositionTestData.SessionId, CompositionTestData.Identity(), CompositionTestData.Input(), options: CompositionTestData.RunOptions(), cancellationToken: TestContext.Current.CancellationToken);

        loop.ReceivedRequests.ShouldHaveSingleItem().Output.ShouldBe(output);
    }

    [Fact]
    public async Task RunAsync_WhenABudgetAuthorityIsRegistered_CompilesItIntoTheRunServices()
    {
        var loop = new RecordingAgentLoop();
        var authority = new NullBudgetAuthority();
        var builder = CompositionTestData.RunnableBuilder(loop);
        _ = builder.Services.Replace(ServiceDescriptor.Singleton<IBudgetAuthority>(authority));
        await using var engine = builder.Build();
        var agent = (await engine.GetAgentAsync(CompositionTestData.AgentId, TestContext.Current.CancellationToken)).RequireResolved();

        _ = await agent.RunAsync<string>(CompositionTestData.SessionId, CompositionTestData.Identity(), CompositionTestData.Input(), options: CompositionTestData.RunOptions(), cancellationToken: TestContext.Current.CancellationToken);

        loop.ReceivedServices.ShouldHaveSingleItem().Budgets.ShouldBeSameAs(authority);
    }

    [Fact]
    public async Task RunAsync_WhenTheDefinitionSelectsABudgetProfile_CarriesItOnTheRequestWithoutInlineLimits()
    {
        var loop = new RecordingAgentLoop();
        var profile = new BudgetProfileKey("tight");
        var definition = CompositionTestData.Definition().WithComponents(budgetProfile: profile);
        var builder = CompositionTestData.RunnableBuilder(loop, definition);
        _ = builder.Services.Replace(ServiceDescriptor.Singleton<IBudgetProfileCatalog>(new StaticBudgetProfileCatalog(profile)));
        await using var engine = builder.Build();
        var agent = (await engine.GetAgentAsync(CompositionTestData.AgentId, TestContext.Current.CancellationToken)).RequireResolved();

        _ = await agent.RunAsync<string>(CompositionTestData.SessionId, CompositionTestData.Identity(), CompositionTestData.Input(), options: CompositionTestData.RunOptions(), cancellationToken: TestContext.Current.CancellationToken);

        var request = loop.ReceivedRequests.ShouldHaveSingleItem();
        request.BudgetProfile.ShouldBe(profile);
    }

    [Fact]
    public async Task RunAsync_WhenCalledTwice_AllocatesADistinctRunIdEachTime()
    {
        var loop = new RecordingAgentLoop();
        await using var engine = CompositionTestData.RunnableBuilder(loop).Build();
        var agent = (await engine.GetAgentAsync(
            CompositionTestData.AgentId,
            TestContext.Current.CancellationToken)).RequireResolved();

        _ = await agent.RunAsync<string>(CompositionTestData.SessionId, CompositionTestData.Identity(), CompositionTestData.Input(), options: CompositionTestData.RunOptions(), cancellationToken: TestContext.Current.CancellationToken);
        _ = await agent.RunAsync<string>(CompositionTestData.SessionId, CompositionTestData.Identity(), CompositionTestData.Input(), options: CompositionTestData.RunOptions(), cancellationToken: TestContext.Current.CancellationToken);

        loop.ReceivedRequests[0].RunId.ShouldNotBe(loop.ReceivedRequests[1].RunId);
    }

    [Fact]
    public async Task RunAsync_WhenOverrideNarrowsTurnLimit_UsesTheNarrowerValue()
    {
        var loop = new RecordingAgentLoop();
        await using var engine = CompositionTestData.RunnableBuilder(loop).Build();
        var agent = (await engine.GetAgentAsync(
            CompositionTestData.AgentId,
            TestContext.Current.CancellationToken)).RequireResolved();

        _ = await agent.RunAsync<string>(
            CompositionTestData.SessionId, CompositionTestData.Identity(), CompositionTestData.Input(),
            options: CompositionTestData.RunOptions(maxTurns: 3),
            cancellationToken: TestContext.Current.CancellationToken);

        loop.ReceivedRequests.ShouldHaveSingleItem().MaxTurns.ShouldBe(3);
    }

    [Fact]
    public async Task RunAsync_WhenOverrideWidensTurnLimit_ThrowsBeforeRunning()
    {
        var loop = new RecordingAgentLoop();
        await using var engine = CompositionTestData.RunnableBuilder(loop).Build();
        var agent = (await engine.GetAgentAsync(
            CompositionTestData.AgentId,
            TestContext.Current.CancellationToken)).RequireResolved();

        var exception = await Should.ThrowAsync<ArgumentOutOfRangeException>(
            async () => await agent.RunAsync<string>(
                CompositionTestData.SessionId, CompositionTestData.Identity(), CompositionTestData.Input(),
                options: CompositionTestData.RunOptions(maxTurns: 99),
                cancellationToken: TestContext.Current.CancellationToken));

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
            TestContext.Current.CancellationToken)).RequireResolved();

        _ = await Should.ThrowAsync<ArgumentOutOfRangeException>(
            async () => await agent.RunAsync<string>(
                CompositionTestData.SessionId, CompositionTestData.Identity(), CompositionTestData.Input(),
                options: CompositionTestData.RunOptions(attemptTimeout: TimeSpan.FromHours(1)),
                cancellationToken: TestContext.Current.CancellationToken));

        loop.ReceivedRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task RunAsync_WhenOptionsIsNull_UsesTheDefinitionsDefaults()
    {
        var loop = new RecordingAgentLoop();
        await using var engine = CompositionTestData.RunnableBuilder(loop).Build();
        var agent = (await engine.GetAgentAsync(
            CompositionTestData.AgentId,
            TestContext.Current.CancellationToken)).RequireResolved();

        _ = await agent.RunAsync<string>(
            CompositionTestData.SessionId, CompositionTestData.Identity(), CompositionTestData.Input(),
            options: null, cancellationToken: TestContext.Current.CancellationToken);

        loop.ReceivedRequests.ShouldHaveSingleItem().MaxTurns.ShouldBe(8);
    }

    [Fact]
    public async Task DisposeAsync_WhenCalledConcurrently_DisposesOwnerOnceAndSharesCompletion()
    {
        var owner = new BlockingAsyncDisposable();
        var engine = new AgentEngine(new AgentEngineRuntime(
            MinimalProvider(), owner, Composition()));

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
        var engine = new AgentEngine(new AgentEngineRuntime(
            MinimalProvider(), owner, Composition()));

        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await engine.DisposeAsync());
        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await engine.DisposeAsync());

        owner.DisposeCount.ShouldBe(1);
    }

    [Fact]
    public async Task DisposeAsync_WhenRequiredSinksExist_DrainsThemBeforeDisposingTheOwnedProvider()
    {
        var order = new List<string>();
        var coordinator = new RecordingDrainCoordinator(order, new RequiredRunEventSinkDrainResult(["audit"], [], []));
        var owner = new OrderRecordingDisposable(order);
        var engine = new AgentEngine(new AgentEngineRuntime(MinimalProvider(coordinator), owner, Composition()));

        await engine.DisposeAsync();

        order.ShouldBe(["drain", "dispose"]);
    }

    [Fact]
    public async Task DisposeAsync_WhenARequiredSinkTimedOut_LogsItAndStillDisposesWithoutThrowing()
    {
        var logger = new RecordingLogger<AgentEngine>();
        var order = new List<string>();
        var coordinator = new RecordingDrainCoordinator(order, new RequiredRunEventSinkDrainResult([], ["otel"], ["audit"]));
        var owner = new OrderRecordingDisposable(order);
        var engine = new AgentEngine(new AgentEngineRuntime(MinimalProvider(coordinator, logger), owner, Composition()));

        await engine.DisposeAsync();

        order.ShouldBe(["drain", "dispose"]);
        var entry = logger.Snapshot().Single(static candidate => candidate.EventId.Id == 18201);
        entry.Level.ShouldBe(LogLevel.Warning);
        entry.Message.ShouldContain("otel");
        entry.Message.ShouldContain("audit");
    }

    [Fact]
    public async Task DisposeAsync_WhenTheDrainLoggerThrows_StillDisposesTheOwnedProvider()
    {
        var logger = new RecordingLogger<AgentEngine> { ThrowOnWrite = true };
        var order = new List<string>();
        var coordinator = new RecordingDrainCoordinator(order, new RequiredRunEventSinkDrainResult([], ["otel"], []));
        var owner = new OrderRecordingDisposable(order);
        var engine = new AgentEngine(new AgentEngineRuntime(MinimalProvider(coordinator, logger), owner, Composition()));

        await engine.DisposeAsync();

        order.ShouldBe(["drain", "dispose"]);
    }

    [Fact]
    public async Task DisposeAsync_WhenNoCoordinatorIsComposed_DisposesWithoutDraining()
    {
        var order = new List<string>();
        var owner = new OrderRecordingDisposable(order);
        var engine = new AgentEngine(new AgentEngineRuntime(MinimalProvider(), owner, Composition()));

        await engine.DisposeAsync();

        order.ShouldBe(["dispose"]);
    }

    private static ServiceProvider MinimalProvider(IRequiredRunEventSinkCoordinator? coordinator = null, ILogger<AgentEngine>? logger = null)
    {
        var services = new ServiceCollection();
        _ = services.AddAgentKit();
        if (coordinator is not null)
        {
            _ = services.AddSingleton(coordinator);
        }

        if (logger is not null)
        {
            _ = services.AddSingleton(logger);
        }

        _ = services.AddSingleton<ISecurityProfileSelector>(new TestSecurityProfileSelector());
        return services.BuildServiceProvider();
    }

    private static AgentCompositionSnapshot Composition() => new(
        new AgentRunProfilePublicationSnapshot([]),
        ComponentRegistrationSnapshot.Capture(new ServiceCollection()));

    private sealed class RecordingDrainCoordinator(List<string> order, RequiredRunEventSinkDrainResult result): IRequiredRunEventSinkCoordinator
    {
        public ValueTask<RequiredRunEventSinkDrainResult> DrainAsync(CancellationToken cancellationToken = default)
        {
            order.Add("drain");
            return ValueTask.FromResult(result);
        }
    }

    private sealed class OrderRecordingDisposable(List<string> order): IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            order.Add("dispose");
            return ValueTask.CompletedTask;
        }
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

    private sealed class NullOutputProcessor: IOutputProcessor
    {
        public ValueTask<OutputProcessingResult> ProcessAsync(
            OutputProcessingRequest request, HookDispatchContext? hooks, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class NullBudgetAuthority: IBudgetAuthority
    {
        public ValueTask<BudgetScopeResult> CreateChildScopeAsync(BudgetScopeRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
