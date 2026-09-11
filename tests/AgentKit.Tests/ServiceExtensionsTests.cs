// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;



/// <summary>Verifies ServiceExtensions behavior and contracts.</summary>
public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddAgentKit_WhenServicesIsNull_ThrowsBeforeRegistration()
    {
        IServiceCollection services = null!;
        var exception = Should.Throw<ArgumentNullException>(services.AddAgentKit);
        exception.ParamName.ShouldBe("services");
    }

    [Fact]
    public void AddAgentKit_WhenCalledTwice_RegistersSingularFacadeDefaultsOnce()
    {
        var services = new ServiceCollection();
        _ = services.AddAgentKit();
        _ = services.AddAgentKit();
        services.Count(static descriptor => descriptor.ServiceType == typeof(AgentEngine)).ShouldBe(1);
        services.Count(static descriptor => descriptor.ServiceType == typeof(TimeProvider)).ShouldBe(1);
    }

    [Fact]
    public void AddAgentKit_WhenRegistered_DoesNotResolveServices()
    {
        var resolutionCount = 0;
        var services = new ServiceCollection();
        _ = services.AddSingleton(_ =>
        {
            resolutionCount++;
            return TimeProvider.System;
        });
        _ = services.AddAgentKit();
        resolutionCount.ShouldBe(0);
    }

    [Fact]
    public async Task AddAgentKit_WhenTimeProviderAlreadyExists_PreservesRegistration()
    {
        var timeProvider = new TrackingTimeProvider();
        var services = new ServiceCollection();
        _ = services.AddSingleton<TimeProvider>(timeProvider);
        _ = services.AddAgentKit();
        _ = services.AddSingleton<IAgentLoop>(new RecordingAgentLoop());
        var definition = CompositionTestData.Definition();
        _ = services.AddAgent(definition);
        CompositionTestData.AddRunProfiles(services, definition);
        await using var provider = CompositionTestData.BuildHostedProvider(services);
        provider.GetRequiredService<AgentEngine>().TimeProvider.ShouldBeSameAs(timeProvider);
    }

    [Fact]
    public async Task ReplaceTimeProvider_WhenDefaultExists_ReplacesItExactlyOnce()
    {
        var timeProvider = new TrackingTimeProvider();
        var services = new ServiceCollection();
        _ = services.AddAgentKit();
        _ = services.AddSingleton<IAgentLoop>(new RecordingAgentLoop());
        var definition = CompositionTestData.Definition();
        _ = services.AddAgent(definition);
        CompositionTestData.AddRunProfiles(services, definition);
        _ = services.ReplaceTimeProvider(timeProvider);
        await using var provider = CompositionTestData.BuildHostedProvider(services);
        services.Count(static descriptor => descriptor.ServiceType == typeof(TimeProvider)).ShouldBe(1);
        provider.GetRequiredService<AgentEngine>().TimeProvider.ShouldBeSameAs(timeProvider);
    }

    [Fact]
    public void ReplaceTimeProvider_WhenInstanceIsNull_ValidatesBeforeMutation()
    {
        var services = new ServiceCollection();
        _ = services.AddAgentKit();
        var exception = Should.Throw<ArgumentNullException>(() => services.ReplaceTimeProvider(null!));
        exception.ParamName.ShouldBe("timeProvider");
        services.Count(static descriptor => descriptor.ServiceType == typeof(TimeProvider)).ShouldBe(1);
    }

    [Fact]
    public void ReplaceTimeProvider_WhenServicesIsNull_ThrowsBeforeRegistration()
    {
        IServiceCollection services = null!;
        var exception = Should.Throw<ArgumentNullException>(() => services.ReplaceTimeProvider(TimeProvider.System));
        exception.ParamName.ShouldBe("services");
    }

    [Fact]
    public async Task AddAgentKit_WhenEngineIsHostManaged_DoesNotDisposeHostServices()
    {
        TrackingTimeProvider? timeProvider = null;
        var services = new ServiceCollection();
        _ = services.AddSingleton<TimeProvider>(_ => timeProvider = new TrackingTimeProvider());
        _ = services.AddAgentKit();
        _ = services.AddSingleton<IAgentLoop>(new RecordingAgentLoop());
        var definition = CompositionTestData.Definition();
        _ = services.AddAgent(definition);
        CompositionTestData.AddRunProfiles(services, definition);
        var provider = CompositionTestData.BuildHostedProvider(services);
        try
        {
            var engine = provider.GetRequiredService<AgentEngine>();
            _ = timeProvider.ShouldNotBeNull();
            await engine.DisposeAsync();
            timeProvider.DisposeCount.ShouldBe(0);
        }
        finally
        {
            await provider.DisposeAsync();
        }

        timeProvider!.DisposeCount.ShouldBe(1);
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

    [Fact]
    public void AddAgent_WhenExactDefinitionIsRegisteredTwice_AddsOneBootstrapContribution()
    {
        var services = new ServiceCollection();
        _ = services.AddAgent(CompositionTestData.Definition());
        _ = services.AddAgent(CompositionTestData.Definition());
        services.Count(descriptor => descriptor.ServiceType == typeof(AgentDefinitionSourceSnapshot)).ShouldBe(1);
        services.Count(descriptor => descriptor.ServiceType == typeof(IAgentDefinitionSource)).ShouldBe(1);
    }

    [Fact]
    public void AddAgent_WhenMatchingBootstrapWasRegisteredFirst_AddsTheMissingSource()
    {
        var services = new ServiceCollection();
        var definition = CompositionTestData.Definition();
        _ = services.AddAgentDefinitionSnapshot(new AgentDefinitionSourceSnapshot(new AgentDefinitionSourceId($"agent:{definition.Id}"), new AgentDefinitionSourceVersion(0), 0, [definition]));
        _ = services.AddAgent(definition);
        services.Count(descriptor => descriptor.ServiceType == typeof(AgentDefinitionSourceSnapshot)).ShouldBe(1);
        services.Count(descriptor => descriptor.ServiceType == typeof(IAgentDefinitionSource)).ShouldBe(1);
    }

    [Fact]
    public void AddAgentDefinitionSnapshot_WhenSameSourceHasDifferentContent_ThrowsBeforeMutation()
    {
        var services = new ServiceCollection();
        var first = new AgentDefinitionSourceSnapshot(new AgentDefinitionSourceId("host"), new AgentDefinitionSourceVersion(1), 0, [CompositionTestData.Definition()]);
        var second = new AgentDefinitionSourceSnapshot(new AgentDefinitionSourceId("host"), new AgentDefinitionSourceVersion(2), 0, [CompositionTestData.Definition(displayName: "other")]);
        _ = services.AddAgentDefinitionSnapshot(first);
        var count = services.Count;
        _ = Should.Throw<InvalidOperationException>(() => services.AddAgentDefinitionSnapshot(second));
        services.Count.ShouldBe(count);
    }
}
