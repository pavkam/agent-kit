// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

public sealed class AgentEngineBuilderTests
{
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
    public void Build_WhenNoAgentIsPublished_ThrowsWithAnEmptyCatalogDiagnostic()
    {
        var builder = AgentEngine.CreateBuilder();
        _ = builder.Services.AddSingleton<IAgentLoop>(new RecordingAgentLoop());

        var exception = Should.Throw<AgentCompositionException>(builder.Build);

        exception.Diagnostics.ShouldContain(
            diagnostic => diagnostic.Code == "agentkit.catalog.empty");
    }

    [Fact]
    public void Build_WhenNoLoopIsRegistered_ThrowsWithAnUnresolvableLoopDiagnostic()
    {
        var builder = AgentEngine.CreateBuilder();
        _ = builder.Services.AddAgent(CompositionTestData.Definition());

        var exception = Should.Throw<AgentCompositionException>(builder.Build);

        exception.Diagnostics.ShouldContain(
            diagnostic => diagnostic.Code == "agentkit.loop.unresolvable");
    }

    [Fact]
    public void Build_WhenSeveralProblemsExist_ReportsAllOfThem()
    {
        var builder = AgentEngine.CreateBuilder();

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
