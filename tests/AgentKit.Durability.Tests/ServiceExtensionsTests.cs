// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Tests;

using Microsoft.Extensions.DependencyInjection;

public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddAgentDurability_WhenServicesIsNull_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;

        var exception = Should.Throw<ArgumentNullException>(() => services.AddAgentDurability());

        exception.ParamName.ShouldBe("services");
    }

    [Fact]
    public void AddAgentDurability_WhenCalled_RegistersTheSingularEngineWideComposition()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();

        _ = services.AddAgentDurability();

        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<IDurableBackendCatalog>().ShouldBeOfType<DurableBackendCatalog>();
        _ = provider.GetRequiredService<IDurableBackendSelector>().ShouldBeOfType<DefaultDurableBackendSelector>();
        _ = provider.GetRequiredService<IDurabilityRuntimeSelector>()
            .ShouldBeOfType<DefaultDurabilityRuntimeSelector>();
        _ = provider.GetRequiredService<IDurableExecutionEventDispatcher>()
            .ShouldBeOfType<DefaultDurableExecutionEventDispatcher>();
        _ = provider.GetRequiredService<IDurabilityProfileCatalog>();
        _ = provider.GetRequiredService<IIdentifierGenerator<CheckpointId>>();
        _ = provider.GetRequiredService<IIdentifierGenerator<WorkerId>>();
        _ = provider.GetRequiredService<TimeProvider>();
    }

    [Fact]
    public void AddAgentDurability_WhenCalled_RegistersNoPersistenceTarget()
    {
        // Process memory is not durable. Selecting a journal, lease manager, or backend is the application's decision.
        var services = new ServiceCollection();
        _ = services.AddLogging();

        _ = services.AddAgentDurability();

        using var provider = services.BuildServiceProvider();
        provider.GetKeyedServices<IDurableOperationJournal>(KeyedService.AnyKey).ShouldBeEmpty();
        provider.GetKeyedServices<IDurableLeaseManager>(KeyedService.AnyKey).ShouldBeEmpty();
        provider.GetKeyedServices<IDurableExecutionBackend>(KeyedService.AnyKey).ShouldBeEmpty();
        provider.GetKeyedServices<IRecoveryPolicy>(KeyedService.AnyKey).ShouldBeEmpty();
    }

    [Fact]
    public void AddAgentDurability_WhenCalledTwice_ReusesTheSameSingularServices()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();

        _ = services.AddAgentDurability();
        _ = services.AddAgentDurability();

        // The coordinator itself is not resolved here: it depends on the application's security authority selector,
        // which durability never registers on the application's behalf.
        services.Count(descriptor => descriptor.ServiceType == typeof(IDurabilityRuntimeSelector)).ShouldBe(1);
        services.Count(descriptor => descriptor.ServiceType == typeof(IDurableExecutionCoordinator)).ShouldBe(1);
        services.Count(descriptor => descriptor.ServiceType == typeof(IDurableBackendCatalog)).ShouldBe(1);
    }

    [Fact]
    public void AddAgentDurability_WhenCalledTwice_AppliesEveryConfigurationCallback()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();

        _ = services.AddAgentDurability(options => options.LeaseDuration = TimeSpan.FromMinutes(3));
        _ = services.AddAgentDurability(
            options => options.UnknownEffectMode = UnknownEffectRecoveryMode.ReconcileWhenSupported);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AgentDurabilityOptions>>().Value;
        options.LeaseDuration.ShouldBe(TimeSpan.FromMinutes(3));
        options.UnknownEffectMode.ShouldBe(UnknownEffectRecoveryMode.ReconcileWhenSupported);
    }

    [Theory]
    [InlineData(0, 10, 1)]
    [InlineData(10, 0, 1)]
    [InlineData(10, 20, 1)]
    [InlineData(10, 5, 0)]
    public void AddAgentDurability_WhenOptionsAreImpossible_FailsAtTheCompositionBoundary(
        int leaseSeconds,
        int renewalSeconds,
        int maximumAttempts)
    {
        // An impossible ceiling is a composition failure, not something to discover mid-operation.
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddAgentDurability(options =>
        {
            options.LeaseDuration = TimeSpan.FromSeconds(leaseSeconds);
            options.LeaseRenewalInterval = TimeSpan.FromSeconds(renewalSeconds);
            options.MaximumRecoveryAttempts = maximumAttempts;
        });

        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<AgentDurabilityOptions>>().Value);
    }

    [Fact]
    public void AddDurabilityProfile_WhenRegistered_ProjectsAResolvableSnapshot()
    {
        using var provider = Compose(services => services.AddDurabilityProfile(
            new DurabilityProfileKey("primary"), ConfigureProfile));

        var catalog = provider.GetRequiredService<IDurabilityProfileCatalog>();
        catalog.TryGet(new DurabilityProfileKey("primary"), out var profile).ShouldBeTrue();
        profile!.BackendKey.ShouldBe(new DurableBackendKey("backend"));
        profile.JournalKey.ShouldBe(new DurableJournalKey("journal"));
        profile.LeaseManagerKey.ShouldBe(new DurableLeaseManagerKey("leases"));
        profile.RecoveryPolicyKey.ShouldBe(new RecoveryPolicyKey("policy"));
    }

    [Fact]
    public void AddDurabilityProfile_WhenTheKeyIsUnregistered_ReportsNoProfile()
    {
        using var provider = Compose(services => services.AddDurabilityProfile(
            new DurabilityProfileKey("primary"), ConfigureProfile));

        var catalog = provider.GetRequiredService<IDurabilityProfileCatalog>();

        catalog.TryGet(new DurabilityProfileKey("absent"), out var profile).ShouldBeFalse();
        profile.ShouldBeNull();
    }

    [Fact]
    public void AddDurabilityProfile_WhenRepeatedForOneKey_RefinesTheAccumulatedOptions()
    {
        using var provider = Compose(services =>
        {
            _ = services.AddDurabilityProfile(new DurabilityProfileKey("primary"), ConfigureProfile);
            return services.AddDurabilityProfile(
                new DurabilityProfileKey("primary"),
                options => options.EnabledOperations.Add(new DurableOperationName("tool.call")));
        });

        var catalog = provider.GetRequiredService<IDurabilityProfileCatalog>();
        catalog.TryGet(new DurabilityProfileKey("primary"), out var profile).ShouldBeTrue();
        profile!.JournalKey.ShouldBe(new DurableJournalKey("journal"));
        profile.EnabledOperations.ShouldBe([new DurableOperationName("tool.call")]);
    }

    [Fact]
    public void ReplaceDurabilityProfile_WhenCalled_DiscardsTheEarlierContributions()
    {
        using var provider = Compose(services =>
        {
            _ = services.AddDurabilityProfile(
                new DurabilityProfileKey("primary"),
                options =>
                {
                    ConfigureProfile(options);
                    options.EnabledOperations.Add(new DurableOperationName("tool.call"));
                });
            return services.ReplaceDurabilityProfile(new DurabilityProfileKey("primary"), ConfigureProfile);
        });

        var catalog = provider.GetRequiredService<IDurabilityProfileCatalog>();
        catalog.TryGet(new DurabilityProfileKey("primary"), out var profile).ShouldBeTrue();
        profile!.EnabledOperations.ShouldBeEmpty();
    }

    [Fact]
    public void AddDurabilityProfile_WhenTheKeyIsDefault_ThrowsArgumentException()
    {
        var services = new ServiceCollection();

        var exception = Should.Throw<ArgumentException>(
            () => services.AddDurabilityProfile(default, ConfigureProfile));

        exception.ParamName.ShouldBe("key");
    }

    [Fact]
    public void AddDurabilityProfile_WhenTheCallbackIsNull_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        var exception = Should.Throw<ArgumentNullException>(
            () => services.AddDurabilityProfile(new DurabilityProfileKey("primary"), null!));

        exception.ParamName.ShouldBe("configure");
    }

    [Theory]
    [InlineData("backend")]
    [InlineData("journal")]
    [InlineData("leases")]
    [InlineData("policy")]
    public void AddDurabilityProfile_WhenARequiredSelectionIsMissing_FailsWhenTheRegistryIsBuilt(string omitted)
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddAgentDurability();
        _ = services.AddDurabilityProfile(new DurabilityProfileKey("primary"), options =>
        {
            options.BackendKey = omitted == "backend" ? default : new DurableBackendKey("backend");
            options.JournalKey = omitted == "journal" ? default : new DurableJournalKey("journal");
            options.LeaseManagerKey = omitted == "leases" ? default : new DurableLeaseManagerKey("leases");
            options.RecoveryPolicyKey = omitted == "policy" ? default : new RecoveryPolicyKey("policy");
        });
        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<ArgumentException>(provider.GetRequiredService<IDurabilityProfileCatalog>);
    }

    [Fact]
    public void AddDurableJournal_WhenRegisteredUnderOneKey_DoesNotShadowAnotherKey()
    {
        using var provider = Compose(services =>
        {
            _ = services.AddDurableJournal<RecordingDurableOperationJournal>(new DurableJournalKey("first"));
            return services.AddDurableJournal<RecordingDurableOperationJournal>(new DurableJournalKey("second"));
        });

        var first = provider.GetKeyedService<IDurableOperationJournal>("first").ShouldNotBeNull();
        var second = provider.GetKeyedService<IDurableOperationJournal>("second").ShouldNotBeNull();
        first.ShouldNotBeSameAs(second);
    }

    [Fact]
    public void ReplaceDurableJournal_WhenCalled_LeavesOnlyOneDescriptorForThatKey()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddAgentDurability();
        _ = services.AddDurableJournal<RecordingDurableOperationJournal>(new DurableJournalKey("primary"));

        _ = services.ReplaceDurableJournal<SecondRecordingJournal>(new DurableJournalKey("primary"));

        services.Count(descriptor =>
                descriptor.ServiceType == typeof(IDurableOperationJournal)
                && Equals(descriptor.ServiceKey, "primary"))
            .ShouldBe(1);
        using var provider = services.BuildServiceProvider();
        _ = provider.GetKeyedService<IDurableOperationJournal>("primary").ShouldBeOfType<SecondRecordingJournal>();
    }

    [Fact]
    public void ReplaceDurableJournal_WhenCalled_LeavesOtherKeysIntact()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddAgentDurability();
        _ = services.AddDurableJournal<RecordingDurableOperationJournal>(new DurableJournalKey("other"));
        _ = services.AddDurableJournal<RecordingDurableOperationJournal>(new DurableJournalKey("primary"));

        _ = services.ReplaceDurableJournal<SecondRecordingJournal>(new DurableJournalKey("primary"));

        using var provider = services.BuildServiceProvider();
        _ = provider.GetKeyedService<IDurableOperationJournal>("other")
            .ShouldBeOfType<RecordingDurableOperationJournal>();
    }

    [Fact]
    public void ReplaceDurableLeaseManager_WhenCalled_LeavesOnlyOneDescriptorForThatKey()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddAgentDurability();
        _ = services.AddSingleton<IIdentifierGenerator<ExecutionLeaseId>, SequentialExecutionLeaseIdGenerator>();
        _ = services.AddDurableLeaseManager<InMemoryDurableLeaseManager>(new DurableLeaseManagerKey("leases"));

        _ = services.ReplaceDurableLeaseManager<InMemoryDurableLeaseManager>(new DurableLeaseManagerKey("leases"));

        services.Count(descriptor =>
                descriptor.ServiceType == typeof(IDurableLeaseManager)
                && Equals(descriptor.ServiceKey, "leases"))
            .ShouldBe(1);
    }

    [Fact]
    public void ReplaceRecoveryPolicy_WhenCalled_LeavesOnlyOneDescriptorForThatKey()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddAgentDurability();
        _ = services.AddRecoveryPolicy<DefaultRecoveryPolicy>(new RecoveryPolicyKey("policy"));

        _ = services.ReplaceRecoveryPolicy<DefaultRecoveryPolicy>(new RecoveryPolicyKey("policy"));

        services.Count(descriptor =>
                descriptor.ServiceType == typeof(IRecoveryPolicy) && Equals(descriptor.ServiceKey, "policy"))
            .ShouldBe(1);
    }

    [Fact]
    public void ReplaceDurabilityBackend_WhenCalled_LeavesOnlyOneDescriptorForThatKey()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddAgentDurability();
        _ = services.AddDurabilityBackend<InMemoryDurableExecutionBackend>(new DurableBackendKey("backend"));

        _ = services.ReplaceDurabilityBackend<InMemoryDurableExecutionBackend>(new DurableBackendKey("backend"));

        services.Count(descriptor =>
                descriptor.ServiceType == typeof(IDurableExecutionBackend)
                && Equals(descriptor.ServiceKey, "backend"))
            .ShouldBe(1);
    }

    [Fact]
    public void AddDurableOperationCodec_WhenRegisteredTwice_KeepsBothContributions()
    {
        using var provider = Compose(services =>
        {
            _ = services.AddDurableOperationCodec<TestState, FirstTestCodec>();
            return services.AddDurableOperationCodec<TestState, SecondTestCodec>();
        });

        provider.GetServices<IDurableOperationCodec<TestState>>().Count().ShouldBe(2);
    }

    [Fact]
    public void ReplaceDurableOperationCodec_WhenCalled_LeavesExactlyOneCodecForThatState()
    {
        using var provider = Compose(services =>
        {
            _ = services.AddDurableOperationCodec<TestState, FirstTestCodec>();
            _ = services.AddDurableOperationCodec<TestState, FirstTestCodec>();
            return services.ReplaceDurableOperationCodec<TestState, SecondTestCodec>();
        });

        var codecs = provider.GetServices<IDurableOperationCodec<TestState>>().ToArray();
        codecs.Length.ShouldBe(1);
        _ = codecs[0].ShouldBeOfType<SecondTestCodec>();
    }

    [Fact]
    public void ReplaceDurableExecutionCoordinator_WhenCalled_ResolvesTheSubstitute()
    {
        using var provider = Compose(services =>
            services.ReplaceDurableExecutionCoordinator<UnreachableCoordinator>());

        _ = provider.GetRequiredService<IDurableExecutionCoordinator>().ShouldBeOfType<UnreachableCoordinator>();
    }

    [Fact]
    public void ReplaceDurabilityRuntimeSelector_WhenCalled_ResolvesTheSubstitute()
    {
        using var provider = Compose(services =>
            services.ReplaceDurabilityRuntimeSelector<UnreachableRuntimeSelector>());

        _ = provider.GetRequiredService<IDurabilityRuntimeSelector>().ShouldBeOfType<UnreachableRuntimeSelector>();
    }

    [Fact]
    public void AddDurableOperationHandler_WhenRegisteredTwice_KeepsBothContributions()
    {
        using var provider = Compose(services =>
        {
            _ = services.AddDurableOperationHandler<FirstTestHandler>();
            return services.AddDurableOperationHandler<SecondTestHandler>();
        });

        provider.GetServices<IDurableOperationHandler>().Count().ShouldBe(2);
    }

    [Fact]
    public void AddInMemoryDurableExecutionBackend_WhenRepeatedForOneKey_IsIdempotent()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddAgentDurability();

        _ = services.AddInMemoryDurableExecutionBackend(new DurableBackendKey("backend"));
        _ = services.AddInMemoryDurableExecutionBackend(new DurableBackendKey("backend"));

        using var provider = services.BuildServiceProvider();
        provider.GetKeyedServices<IDurableExecutionBackend>(KeyedService.AnyKey).Count().ShouldBe(1);
    }

    private static void ConfigureProfile(DurabilityProfileOptions options)
    {
        options.BackendKey = new DurableBackendKey("backend");
        options.JournalKey = new DurableJournalKey("journal");
        options.LeaseManagerKey = new DurableLeaseManagerKey("leases");
        options.RecoveryPolicyKey = new RecoveryPolicyKey("policy");
    }

    private static ServiceProvider Compose(Func<IServiceCollection, IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddAgentDurability();
        _ = configure(services);
        return services.BuildServiceProvider();
    }

    /// <summary>A second journal type so replacement can be observed by resolved implementation type.</summary>
    private sealed class SecondRecordingJournal: IDurableOperationJournal
    {
        public ComponentId SecurityAudience { get; } = new("test.second.journal");

        public ValueTask<DurableRecordResult> RecordStartAsync(
            AuthorizedDurableRequest<DurableOperationStart> start,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The substitute journal is never written to.");

        public ValueTask<DurableRecordResult> RecordCheckpointAsync(
            AuthorizedDurableRequest<DurableCheckpoint> checkpoint,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The substitute journal is never written to.");

        public ValueTask<DurableRecordResult> RecordTerminalAsync(
            AuthorizedDurableRequest<DurableOperationResult> result,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The substitute journal is never written to.");

        public ValueTask<DurableRecordResult> RecordWaitingAsync(
            AuthorizedDurableRequest<DurableOperationWaiting> waiting,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The substitute journal is never written to.");

        public ValueTask<RecoveryEvidenceResult> LoadEvidenceAsync(
            AuthorizedDurableRequest<DurableOperationAddress> address,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The substitute journal is never read from.");
    }

    /// <summary>The durable state used to exercise codec registration.</summary>
    private sealed record TestState(int Value);

    private sealed class FirstTestCodec: IDurableOperationCodec<TestState>
    {
        public DurableOperationName OperationName { get; } = new("tool.call");

        public DurableOperationVersion Version { get; } = new("v1");

        public OperationPayload Encode(TestState value) => DurableJournalTestData.Payload();

        public DurableDecodeResult<TestState> Decode(OperationPayload payload) =>
            new DurableDecoded<TestState>(new TestState(1));
    }

    private sealed class SecondTestCodec: IDurableOperationCodec<TestState>
    {
        public DurableOperationName OperationName { get; } = new("tool.call");

        public DurableOperationVersion Version { get; } = new("v2");

        public OperationPayload Encode(TestState value) => DurableJournalTestData.Payload();

        public DurableDecodeResult<TestState> Decode(OperationPayload payload) =>
            new DurableDecoded<TestState>(new TestState(2));
    }

    private sealed class FirstTestHandler: IDurableOperationHandler
    {
        public DurableOperationName OperationName { get; } = new("first.operation");

        public ValueTask<DurableOperationResult> InvokeAsync(
            RecoverableOperationDescriptor operation,
            IExecutionLease lease,
            HookDispatchContext? hooks,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The substitute handler is never invoked.");
        public ValueTask<DurableOperationResult> InvokeAsync(DurableInvocationContext context, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class SecondTestHandler: IDurableOperationHandler
    {
        public DurableOperationName OperationName { get; } = new("second.operation");

        public ValueTask<DurableOperationResult> InvokeAsync(
            RecoverableOperationDescriptor operation,
            IExecutionLease lease,
            HookDispatchContext? hooks,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The substitute handler is never invoked.");
        public ValueTask<DurableOperationResult> InvokeAsync(DurableInvocationContext context, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class UnreachableCoordinator: IDurableExecutionCoordinator
    {
        public Task<DurableOperationResult> ExecuteAsync(
            RecoverableOperationDescriptor operation,
            HookDispatchContext? hooks,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The substitute coordinator is never invoked.");

        public Task<DurableOperationResult> RecoverAsync(
            DurableOperationAddress address,
            DurableExecutionContext context,
            HookDispatchContext? hooks,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The substitute coordinator is never invoked.");
    }

    private sealed class UnreachableRuntimeSelector: IDurabilityRuntimeSelector
    {
        public ValueTask<DurabilityRuntimeActivationResult> ActivateAsync(
            DurableExecutionContext context,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The substitute selector is never invoked.");
    }
}
