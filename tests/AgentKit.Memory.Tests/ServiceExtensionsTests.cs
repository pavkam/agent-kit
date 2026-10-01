// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Tests;

/// <summary>Verifies registration idempotence, duplicate rejection, replacement, option validation, and the no-default-store rule.</summary>
public sealed class ServiceExtensionsTests
{
    private sealed class OtherStore: IMemoryStore
    {
        public MemoryStoreDescriptor Descriptor => throw new NotSupportedException();

        public ValueTask<MemoryWriteResult> WriteAsync(MemoryWriteRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask<MemoryReadResult> ReadAsync(MemoryReadRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask<MemoryListResult> ListAsync(MemoryListRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask<MemoryTransitionResult> TransitionAsync(MemoryTransitionRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask<MemoryDeleteResult> DeleteAsync(MemoryDeleteRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class Policy: IMemoryPolicy
    {
        public ValueTask<MemoryPolicyDecision> EvaluateAsync(MemoryProposal proposal, MemoryPolicyContext context, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class OtherPolicy: IMemoryPolicy
    {
        public ValueTask<MemoryPolicyDecision> EvaluateAsync(MemoryProposal proposal, MemoryPolicyContext context, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class Sink: IMemoryEventSink
    {
        public ValueTask PublishAsync(MemoryEvent memoryEvent, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }

    private sealed class Chunker: IDocumentChunker
    {
        public ChunkerVersion Version => new("custom");

        public ImmutableArray<DocumentChunk> Chunk(DocumentId documentId, DocumentVersion version, string text) => [];
    }

    [Fact]
    public void AddAgentMemory_WhenServicesIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => ((IServiceCollection) null!).AddAgentMemory()).ParamName.ShouldBe("services");

    [Fact]
    public void AddAgentMemory_WhenCalledTwice_RegistersEachSingularServiceOnce()
    {
        var services = new ServiceCollection();

        _ = services.AddAgentMemory().AddAgentMemory();

        services.Count(static descriptor => descriptor.ServiceType == typeof(IMemoryCoordinator)).ShouldBe(1);
        services.Count(static descriptor => descriptor.ServiceType == typeof(IRetrievalPipeline)).ShouldBe(1);
        services.Count(static descriptor => descriptor.ServiceType == typeof(MemoryPolicyDeclaration)).ShouldBe(1);
    }

    [Fact]
    public void AddAgentMemory_WhenNothingElseIsRegistered_InstallsNoStoreIndexOrSource()
    {
        var services = new ServiceCollection();

        _ = services.AddAgentMemory();

        services.Any(static descriptor => descriptor.IsKeyedService && (descriptor.ServiceType == typeof(IMemoryStore) || descriptor.ServiceType == typeof(IDocumentStore)
            || descriptor.ServiceType == typeof(IVectorIndex) || descriptor.ServiceType == typeof(IRetrievalSource))).ShouldBeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void AddAgentMemory_WhenItemCeilingIsNotPositive_FailsOptionValidation(int value)
    {
        var services = new ServiceCollection();
        _ = services.AddAgentMemory(options => options.MaximumRetrievedItems = value);
        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<AgentMemoryOptions>>().Value);
    }

    [Fact]
    public void AddAgentMemory_WhenGrantLifetimeIsNotPositive_FailsOptionValidation()
    {
        var services = new ServiceCollection();
        _ = services.AddAgentMemory(options => options.GrantLifetime = TimeSpan.Zero);
        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<AgentMemoryOptions>>().Value);
    }

    [Fact]
    public void AddAgentMemory_WhenBytesPerTokenIsNotFinite_FailsOptionValidation()
    {
        var services = new ServiceCollection();
        _ = services.AddAgentMemory(options => options.EstimatedBytesPerToken = double.NaN);
        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<AgentMemoryOptions>>().Value);
    }

    [Fact]
    public void AddAgentMemory_WhenAcceptanceModeIsUndefined_FailsOptionValidation()
    {
        var services = new ServiceCollection();
        _ = services.AddAgentMemory(options => options.AcceptanceMode = (MemoryAcceptanceMode) 99);
        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<AgentMemoryOptions>>().Value);
    }

    [Fact]
    public void AddAgentMemory_WhenDefaults_UseTheFailClosedAcceptanceMode()
    {
        using var provider = new ServiceCollection().AddAgentMemory().BuildServiceProvider();

        provider.GetRequiredService<IOptions<AgentMemoryOptions>>().Value.AcceptanceMode.ShouldBe(MemoryAcceptanceMode.RequireExplicitPolicyAllow);
    }

    [Fact]
    public void AddMemoryStore_WhenSameTypeRegisteredTwice_IsIdempotent()
    {
        var services = new ServiceCollection();

        _ = services.AddMemoryStore<OtherStore>(new MemoryStoreKey("a")).AddMemoryStore<OtherStore>(new MemoryStoreKey("a"));

        services.Count(static descriptor => descriptor.IsKeyedService && descriptor.ServiceType == typeof(IMemoryStore)).ShouldBe(1);
    }

    [Fact]
    public void AddMemoryStore_WhenADifferentTypeIsRegisteredUnderTheSameKey_Throws()
    {
        var services = new ServiceCollection().AddInMemoryMemoryStore(new MemoryStoreKey("a"));

        _ = Should.Throw<InvalidOperationException>(() => services.AddMemoryStore<OtherStore>(new MemoryStoreKey("a")));
    }

    [Fact]
    public void ReplaceMemoryStore_WhenKeyIsRegistered_ReplacesTheRegistration()
    {
        var services = new ServiceCollection().AddMemoryStore<OtherStore>(new MemoryStoreKey("a"));

        _ = services.ReplaceMemoryStore<OtherStore>(new MemoryStoreKey("a"));

        services.Count(static descriptor => descriptor.IsKeyedService && descriptor.ServiceType == typeof(IMemoryStore)).ShouldBe(1);
    }

    [Fact]
    public void KeyedRegistrations_WhenKeyIsBlank_ThrowArgumentException()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentException>(() => services.AddMemoryStore<OtherStore>(default)).ParamName.ShouldBe("key");
        Should.Throw<ArgumentException>(() => services.AddRetrievalSource<RetrievalPipelineTests.FaultySource>(default)).ParamName.ShouldBe("key");
    }

    [Fact]
    public void AddMemoryProfile_WhenArgumentsAreInvalid_Throws()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentException>(() => services.AddMemoryProfile(default, static _ => { })).ParamName.ShouldBe("key");
        Should.Throw<ArgumentNullException>(() => services.AddMemoryProfile(new MemoryProfileKey("p"), null!)).ParamName.ShouldBe("configure");
        Should.Throw<ArgumentException>(() => services.ReplaceMemoryProfile(default, static _ => { })).ParamName.ShouldBe("key");
    }

    [Fact]
    public void AddMemoryPolicy_WhenIdenticalRegistrationRepeats_IsIdempotent()
    {
        var services = new ServiceCollection();
        var registration = new MemoryPolicyRegistration(MemoryHarness.PolicyProfile, new ComponentId("p"), 0, ServiceLifetime.Singleton);

        _ = services.AddMemoryPolicy<Policy>(registration).AddMemoryPolicy<Policy>(registration);

        services.Count(static descriptor => descriptor.ServiceType == typeof(MemoryPolicyDeclaration)).ShouldBe(2);
    }

    [Fact]
    public void AddMemoryPolicy_WhenADifferentPolicyUsesTheSameIdentity_Throws()
    {
        var services = new ServiceCollection();
        var registration = new MemoryPolicyRegistration(MemoryHarness.PolicyProfile, new ComponentId("p"), 0, ServiceLifetime.Singleton);
        _ = services.AddMemoryPolicy<Policy>(registration);

        _ = Should.Throw<InvalidOperationException>(() => services.AddMemoryPolicy<OtherPolicy>(registration));
    }

    [Fact]
    public void ReplaceMemoryPolicy_WhenIdentityExists_ReplacesTheDeclaration()
    {
        var services = new ServiceCollection();
        var registration = new MemoryPolicyRegistration(MemoryHarness.PolicyProfile, new ComponentId("p"), 0, ServiceLifetime.Singleton);
        _ = services.AddMemoryPolicy<Policy>(registration).ReplaceMemoryPolicy<OtherPolicy>(registration);

        services.Where(static descriptor => descriptor.ServiceType == typeof(MemoryPolicyDeclaration))
            .Select(static descriptor => ((MemoryPolicyDeclaration) descriptor.ImplementationInstance!).PolicyType)
            .ShouldContain(typeof(OtherPolicy));
    }

    [Fact]
    public void AddMemoryEventSink_WhenADifferentRegistrationUsesTheSameIdentity_Throws()
    {
        var services = new ServiceCollection();
        _ = services.AddMemoryEventSink<Sink>(new MemoryEventSinkRegistration(new ComponentId("s"), 0, MemoryEventDelivery.Observational, ServiceLifetime.Singleton));

        _ = Should.Throw<InvalidOperationException>(() =>
            services.AddMemoryEventSink<Sink>(new MemoryEventSinkRegistration(new ComponentId("s"), 1, MemoryEventDelivery.Observational, ServiceLifetime.Singleton)));
    }

    [Fact]
    public void AddMemoryEventSink_WhenRegistrationIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new ServiceCollection().AddMemoryEventSink<Sink>(null!)).ParamName.ShouldBe("registration");

    [Fact]
    public void ReplaceDocumentChunker_WhenCalled_ReplacesTheDefaultChunker()
    {
        using var provider = new ServiceCollection().AddAgentMemory().ReplaceDocumentChunker<Chunker>().BuildServiceProvider();

        _ = provider.GetRequiredService<IDocumentChunker>().ShouldBeOfType<Chunker>();
    }

    [Fact]
    public void AddAgentMemory_WhenResolved_ProvidesTheDeterministicChunkerByDefault()
    {
        using var provider = new ServiceCollection().AddAgentMemory().BuildServiceProvider();

        _ = provider.GetRequiredService<IDocumentChunker>().ShouldBeOfType<DeterministicTextChunker>();
    }

    [Fact]
    public void AddRetrievalSourceRegistrations_WhenCalled_RegisterTheKeyedSources()
    {
        var services = new ServiceCollection().AddDurableMemoryRetrievalSource().AddDocumentRetrievalSource();

        services.Count(static descriptor => descriptor.IsKeyedService && descriptor.ServiceType == typeof(IRetrievalSource)).ShouldBe(2);
        MemoryRetrievalSourceKeys.DurableMemory.Value.ShouldNotBe(MemoryRetrievalSourceKeys.Documents.Value);
    }

    [Fact]
    public void AddQueryRewriter_WhenRegistered_DeclaresTheRewriterForProfileCompilation()
    {
        var services = new ServiceCollection();

        _ = services.AddQueryRewriter<NoRewriteQueryRewriter>(new QueryRewriterDescriptor(new QueryRewriterKey("custom"), new QueryRewriterVersion("2")));

        services.Any(static descriptor => descriptor.ServiceType == typeof(QueryRewriterDeclaration)
            && descriptor.ImplementationInstance is QueryRewriterDeclaration { Descriptor.Key.Value: "custom" }).ShouldBeTrue();
    }
}
