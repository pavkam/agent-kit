// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

public sealed class CatalogBootstrapTests
{
    [Fact]
    public void HostManagedEngine_WhenConfiguredSourceHasNoBootstrapSnapshot_RejectsNotReadyWithoutReading()
    {
        ThrowingBootstrapTestSource.Reset();
        var services = new ServiceCollection();
        _ = services.AddAgentKit();
        _ = services.AddSingleton<IAgentLoop>(new RecordingAgentLoop());
        _ = services.AddAgentDefinitionSource<ThrowingBootstrapTestSource>();
        using var provider = CompositionTestData.BuildHostedProvider(services);

        var exception = Should.Throw<AgentCompositionException>(provider.GetRequiredService<AgentEngine>);

        exception.Diagnostics.ShouldContain(diagnostic => diagnostic.Code == "agentkit.catalog.not-ready");
        ThrowingBootstrapTestSource.Reads.ShouldBe(0);
    }

    [Fact]
    public async Task HostManagedEngine_WhenCompleteBootstrapProvided_NeverCallsSourceReadAsync()
    {
        ThrowingBootstrapTestSource.Reset();
        var services = new ServiceCollection();
        _ = services.AddAgentKit();
        _ = services.AddSingleton<IAgentLoop>(new RecordingAgentLoop());
        _ = services.AddAgentDefinitionSource<ThrowingBootstrapTestSource>();
        var definition = CompositionTestData.Definition();
        _ = services.AddAgentDefinitionSnapshot(new AgentDefinitionSourceSnapshot(
            new AgentDefinitionSourceId("test-source"), new AgentDefinitionSourceVersion(1), 0, [definition]));
        CompositionTestData.AddRunProfiles(services, definition);
        await using var provider = CompositionTestData.BuildHostedProvider(services);

        var engine = provider.GetRequiredService<AgentEngine>();

        _ = engine.ShouldNotBeNull();
        ThrowingBootstrapTestSource.Reads.ShouldBe(0);
    }

    [Fact]
    public void Build_WhenConfiguredSourceHasNoBootstrapSnapshot_RejectsNotReadyWithoutReading()
    {
        ThrowingBootstrapTestSource.Reset();
        var builder = AgentEngine.CreateBuilder();
        _ = builder.Services.AddSingleton<IAgentLoop>(new RecordingAgentLoop());
        _ = builder.Services.AddAgentDefinitionSource<ThrowingBootstrapTestSource>();

        var exception = Should.Throw<AgentCompositionException>(builder.Build);

        exception.Diagnostics.ShouldContain(diagnostic => diagnostic.Code == "agentkit.catalog.not-ready");
        ThrowingBootstrapTestSource.Reads.ShouldBe(0);
    }

    [Fact]
    public async Task Build_WhenCompleteBootstrapProvided_NeverCallsSourceReadAsync()
    {
        ThrowingBootstrapTestSource.Reset();
        var definition = CompositionTestData.Definition();
        var builder = AgentEngine.CreateBuilder();
        _ = builder.Services.AddSingleton<IAgentLoop>(new RecordingAgentLoop());
        _ = builder.Services.AddAgentDefinitionSource<ThrowingBootstrapTestSource>();
        _ = builder.Services.AddAgentDefinitionSnapshot(new AgentDefinitionSourceSnapshot(
            new AgentDefinitionSourceId("test-source"), new AgentDefinitionSourceVersion(1), 0, [definition]));
        CompositionTestData.AddRunProfiles(builder.Services, definition);

        await using var engine = builder.Build();

        _ = engine.ShouldNotBeNull();
        ThrowingBootstrapTestSource.Reads.ShouldBe(0);
    }

    [Fact]
    public async Task Build_WhenAgentIsRegistered_UsesMaterializedBootstrapWithoutReadingSource()
    {
        var builder = AgentEngine.CreateBuilder();
        _ = builder.Services.AddSingleton<IAgentLoop>(new RecordingAgentLoop());
        var definition = CompositionTestData.Definition();
        _ = builder.Services.AddAgent(definition);
        CompositionTestData.AddRunProfiles(builder.Services, definition);

        await using var engine = builder.Build();

        _ = engine.ShouldNotBeNull();
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
        _ = services.AddAgentDefinitionSnapshot(new AgentDefinitionSourceSnapshot(
            new AgentDefinitionSourceId($"agent:{definition.Id}"),
            new AgentDefinitionSourceVersion(0),
            0,
            [definition]));

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
