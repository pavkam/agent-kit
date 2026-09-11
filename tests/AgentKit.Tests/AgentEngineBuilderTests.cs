// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

public sealed class AgentEngineBuilderTests
{
    [Fact]
    public void CompositionOptions_WhenAssignedNull_ThrowsExactArgumentNullException()
    {
        var builder = AgentEngine.CreateBuilder();

        var exception = Should.Throw<ArgumentNullException>(() => builder.CompositionOptions = null!);

        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void CreateBuilder_WhenCalled_ReturnsIndependentMutableBuilders()
    {
        var first = AgentEngine.CreateBuilder();
        var second = AgentEngine.CreateBuilder();

        first.ShouldNotBeSameAs(second);
        first.Services.ShouldNotBeSameAs(second.Services);

        _ = first.Services.AddSingleton<ScopedDependency>();
        second.Services.Any(static descriptor => descriptor.ServiceType == typeof(ScopedDependency)).ShouldBeFalse();
    }

    [Fact]
    public void Build_WhenRequiredTimeProviderWasRemoved_ThrowsBeforeReturningEngine()
    {
        var builder = CompositionTestData.RunnableBuilder();
        _ = builder.Services.RemoveAll<TimeProvider>();

        var exception = Should.Throw<AgentCompositionException>(builder.Build);

        exception.Diagnostics.ShouldContain(
            diagnostic => diagnostic.Code == "agentkit.time.missing");
    }

    [Fact]
    public void Build_WhenRequiredSecurityGrantStoreWasRemoved_RejectsBeforeApplicationFactories()
    {
        var applicationFactoryCalls = 0;
        var builder = CompositionTestData.RunnableBuilder();
        _ = builder.Services.RemoveAll<ISecurityGrantStore>();
        _ = builder.Services.AddSingleton(
            _ =>
            {
                applicationFactoryCalls++;
                return new ScopedDependency();
            });

        var exception = Should.Throw<AgentCompositionException>(builder.Build);

        exception.Diagnostics.ShouldContain(
            static diagnostic => diagnostic.Code == "agentkit.security-grant-store.missing");
        applicationFactoryCalls.ShouldBe(0);
    }

    [Fact]
    public void Build_WhenSecurityGrantStoreIsDuplicated_RejectsInsteadOfUsingLastRegistration()
    {
        var storeFactoryCalls = 0;
        var builder = CompositionTestData.RunnableBuilder();
        _ = builder.Services.AddSingleton<ISecurityGrantStore>(
            _ =>
            {
                storeFactoryCalls++;
                throw new InvalidOperationException("Composition validation must not invoke a duplicate store factory.");
            });

        var exception = Should.Throw<AgentCompositionException>(builder.Build);

        exception.Diagnostics.ShouldContain(
            static diagnostic => diagnostic.Code == "agentkit.security-grant-store.ambiguous");
        storeFactoryCalls.ShouldBe(0);
    }

    [Fact]
    public void Build_WhenNoAgentIsPublished_ThrowsWithAnEmptyCatalogDiagnostic()
    {
        var builder = AgentEngine.CreateBuilder();
        CompositionTestData.AddRequiredSecurityGrantStore(builder.Services);
        _ = builder.Services.AddSingleton<ISecurityProfileSelector>(new TestSecurityProfileSelector());
        _ = builder.Services.AddSingleton<IAgentLoop>(new RecordingAgentLoop());

        var exception = Should.Throw<AgentCompositionException>(builder.Build);

        exception.Diagnostics.ShouldContain(
            diagnostic => diagnostic.Code == "agentkit.catalog.empty");
    }

    [Fact]
    public void Build_WhenNoLoopIsRegistered_ThrowsWithAnUnresolvableLoopDiagnostic()
    {
        var builder = AgentEngine.CreateBuilder();
        CompositionTestData.AddRequiredSecurityGrantStore(builder.Services);
        _ = builder.Services.AddAgent(CompositionTestData.Definition());

        var exception = Should.Throw<AgentCompositionException>(builder.Build);

        exception.Diagnostics.ShouldContain(
            diagnostic => diagnostic.Code == "agentkit.loop.unresolvable");
    }

    [Fact]
    public async Task Build_WhenLoopUsesFactory_DoesNotActivateLoopUntilRunStarts()
    {
        var factoryCalls = 0;
        var definition = CompositionTestData.Definition();
        var builder = AgentEngine.CreateBuilder();
        CompositionTestData.AddRunProfiles(builder.Services, definition);
        _ = builder.Services.AddAgent(definition);
        _ = builder.Services.AddScoped<IAgentLoop>(
            _ =>
            {
                factoryCalls++;
                return new RecordingAgentLoop();
            });

        await using var engine = builder.Build();

        factoryCalls.ShouldBe(0);

        var agent = (await engine.GetAgentAsync(definition.Id, TestContext.Current.CancellationToken))!;
        _ = await agent.RunAsync(CompositionTestData.RunOptions(), TestContext.Current.CancellationToken);

        factoryCalls.ShouldBe(1);
    }

    [Fact]
    public void Build_WhenLoopRegistrationIsDuplicated_RejectsWithoutActivatingEitherFactory()
    {
        var factoryCalls = 0;
        var definition = CompositionTestData.Definition();
        var builder = AgentEngine.CreateBuilder();
        CompositionTestData.AddRunProfiles(builder.Services, definition);
        _ = builder.Services.AddAgent(definition);
        _ = builder.Services.AddScoped<IAgentLoop>(
            _ =>
            {
                factoryCalls++;
                return new RecordingAgentLoop();
            });
        _ = builder.Services.AddScoped<IAgentLoop>(
            _ =>
            {
                factoryCalls++;
                return new RecordingAgentLoop();
            });

        var exception = Should.Throw<AgentCompositionException>(builder.Build);

        exception.Diagnostics.ShouldContain(
            static diagnostic => diagnostic.Code == "agentkit.loop.ambiguous");
        factoryCalls.ShouldBe(0);
    }

    [Fact]
    public void Build_WhenLoopConstructorDependencyIsMissing_RejectsWithoutConstructingLoop()
    {
        MissingDependencyAgentLoop.ConstructorCalls = 0;
        var definition = CompositionTestData.Definition();
        var builder = AgentEngine.CreateBuilder();
        CompositionTestData.AddRunProfiles(builder.Services, definition);
        _ = builder.Services.AddAgent(definition);
        _ = builder.Services.AddScoped<IAgentLoop, MissingDependencyAgentLoop>();

        var exception = Should.Throw<AggregateException>(builder.Build);

        exception.ToString().ShouldContain(nameof(UnregisteredLoopDependency));
        MissingDependencyAgentLoop.ConstructorCalls.ShouldBe(0);
    }

    [Fact]
    public void Build_WhenOnlyKeyedLoopIsRegistered_RejectsCurrentUnkeyedRuntime()
    {
        var factoryCalls = 0;
        var definition = CompositionTestData.Definition();
        var builder = AgentEngine.CreateBuilder();
        CompositionTestData.AddRunProfiles(builder.Services, definition);
        _ = builder.Services.AddAgent(definition);
        _ = builder.Services.AddKeyedScoped<IAgentLoop>(
            "selected",
            (_, _) =>
            {
                factoryCalls++;
                return new RecordingAgentLoop();
            });

        var exception = Should.Throw<AgentCompositionException>(builder.Build);

        exception.Diagnostics.ShouldContain(
            static diagnostic => diagnostic.Code == "agentkit.loop.unresolvable");
        factoryCalls.ShouldBe(0);
    }

    [Fact]
    public async Task HostedResolution_WhenLoopUsesFactory_DoesNotActivateLoop()
    {
        var factoryCalls = 0;
        var definition = CompositionTestData.Definition();
        var services = new ServiceCollection();
        _ = services.AddAgentKit();
        CompositionTestData.AddRunProfiles(services, definition);
        _ = services.AddAgent(definition);
        _ = services.AddScoped<IAgentLoop>(
            _ =>
            {
                factoryCalls++;
                return new RecordingAgentLoop();
            });
        await using var provider = CompositionTestData.BuildHostedProvider(services);

        _ = provider.GetRequiredService<AgentEngine>();

        factoryCalls.ShouldBe(0);
    }

    [Fact]
    public async Task Build_WhenLoopInstanceIsSupplied_DoesNotDisposeItDuringReadiness()
    {
        var loop = new DisposableAgentLoop();
        var definition = CompositionTestData.Definition();
        var builder = AgentEngine.CreateBuilder();
        CompositionTestData.AddRunProfiles(builder.Services, definition);
        _ = builder.Services.AddAgent(definition);
        _ = builder.Services.AddSingleton<IAgentLoop>(loop);

        await using var engine = builder.Build();

        loop.DisposeCount.ShouldBe(0);
    }

    [Fact]
    public void Build_WhenSeveralProblemsExist_ReportsAllOfThem()
    {
        var builder = AgentEngine.CreateBuilder();
        CompositionTestData.AddRequiredSecurityGrantStore(builder.Services);

        var exception = Should.Throw<AgentCompositionException>(builder.Build);

        exception.Diagnostics.Length.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void Build_WhenSingletonCapturesScopedService_ThrowsBeforeReturningEngine()
    {
        var builder = CompositionTestData.RunnableBuilder();
        _ = builder.Services.AddScoped<ScopedDependency>();
        _ = builder.Services.AddSingleton<SingletonCapturingScoped>();

        var exception = Should.Throw<AggregateException>(builder.Build);

        exception.ToString().ShouldContain("Cannot consume scoped service");
    }

    [Fact]
    public async Task Build_WhenEngineIsDisposed_DisposesOwnedProviderExactlyOnce()
    {
        TrackingTimeProvider? timeProvider = null;
        var builder = CompositionTestData.RunnableBuilder();
        _ = builder.Services.RemoveAll<TimeProvider>();
        _ = builder.Services.AddSingleton<TimeProvider>(
            _ => timeProvider = new TrackingTimeProvider());

        var engine = builder.Build();

        _ = timeProvider.ShouldNotBeNull();
        timeProvider.DisposeCount.ShouldBe(0);

        await engine.DisposeAsync();
        await engine.DisposeAsync();

        timeProvider.DisposeCount.ShouldBe(1);
    }

    [Fact]
    public async Task Build_WhenBuilderChangesLater_KeepsEngineCompositionImmutable()
    {
        var firstTimeProvider = new TrackingTimeProvider();
        var secondTimeProvider = new TrackingTimeProvider();
        var builder = CompositionTestData.RunnableBuilder();
        _ = builder.Services.ReplaceTimeProvider(firstTimeProvider);
        var engine = builder.Build();

        _ = builder.Services.ReplaceTimeProvider(secondTimeProvider);

        engine.TimeProvider.ShouldBeSameAs(firstTimeProvider);
        await engine.DisposeAsync();
        firstTimeProvider.DisposeCount.ShouldBe(0);
    }

    private sealed class ScopedDependency;

    private sealed class UnregisteredLoopDependency;

    private sealed class MissingDependencyAgentLoop: IAgentLoop
    {
        public MissingDependencyAgentLoop(UnregisteredLoopDependency dependency)
        {
            ArgumentNullException.ThrowIfNull(dependency);
            ConstructorCalls++;
        }

        public static int ConstructorCalls { get; set; }

        public Task<AgentLoopResult> RunAsync(
            AgentRunRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class DisposableAgentLoop: IAgentLoop, IDisposable
    {
        public int DisposeCount { get; private set; }

        public Task<AgentLoopResult> RunAsync(
            AgentRunRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void Dispose() => DisposeCount++;
    }

    private sealed class SingletonCapturingScoped(ScopedDependency dependency)
    {
        public ScopedDependency Dependency { get; } = dependency;
    }

    private sealed class TrackingTimeProvider: TimeProvider, IAsyncDisposable
    {
        private int _disposeCount;

        public int DisposeCount => _disposeCount;

        public ValueTask DisposeAsync()
        {
            _ = Interlocked.Increment(ref _disposeCount);
            return ValueTask.CompletedTask;
        }
    }
}
