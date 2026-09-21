// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

using AgentKit.TestSupport;

using Microsoft.Extensions.Logging;

/// <summary>Verifies AgentEngineBuilder behavior and contracts.</summary>
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
        exception.Diagnostics.ShouldContain(diagnostic => diagnostic.Code == "agentkit.time.missing");
    }

    [Fact]
    public void Build_WhenRequiredServiceDescriptorExistsButItsFactoryProducesNull_RejectsAsMissing()
    {
        // ValidateComponentRegistrations proves exactly one unkeyed TimeProvider descriptor exists from
        // metadata alone, without invoking it. AgentCompositionValidator.Validate's own Resolve<TService>
        // then actually asks the built provider for the service; this is the only way its "missing" branch
        // is reachable, since a single descriptor whose factory produces null still passes the earlier count-only
        // metadata check.
        var builder = CompositionTestData.RunnableBuilder();
        _ = builder.Services.Replace(ServiceDescriptor.Singleton<TimeProvider>(static _ => null!));

        var exception = Should.Throw<AgentCompositionException>(builder.Build);

        exception.Diagnostics.ShouldContain(diagnostic => diagnostic.Code == "agentkit.time.missing");
    }

    [Fact]
    public void Build_WhenRequiredSecurityGrantStoreWasRemoved_RejectsBeforeApplicationFactories()
    {
        var applicationFactoryCalls = 0;
        var builder = CompositionTestData.RunnableBuilder();
        _ = builder.Services.RemoveAll<ISecurityGrantStore>();
        _ = builder.Services.AddSingleton(_ =>
        {
            applicationFactoryCalls++;
            return new ScopedDependency();
        });
        var exception = Should.Throw<AgentCompositionException>(builder.Build);
        exception.Diagnostics.ShouldContain(static diagnostic => diagnostic.Code == "agentkit.security-grant-store.missing");
        applicationFactoryCalls.ShouldBe(0);
    }

    [Fact]
    public void Build_WhenCompositionFailsAfterActivatingAnAsyncDisposableOnlySingleton_PreservesTheOriginalCompositionException()
    {
        // ValidateComponentRegistrations (metadata-only) requires exactly one TimeProvider descriptor
        // to exist but never resolves it; AgentCompositionValidator.Validate's later Resolve<TimeProvider>
        // call actually activates it, well before the empty-catalog check below runs. The build's
        // cleanup path must dispose that already-activated async-only singleton without letting a
        // disposal failure (ServiceProvider.Dispose() throws InvalidOperationException for an
        // IAsyncDisposable-only singleton) replace the real AgentCompositionException the caller
        // needs to see.
        var builder = AgentEngine.CreateBuilder();
        _ = builder.Services.RemoveAll<TimeProvider>();
        _ = builder.Services.AddSingleton<TimeProvider, TrackingTimeProvider>();
        CompositionTestData.AddRequiredSecurityGrantStore(builder.Services);
        CompositionTestData.AddHookKernelForEngineValidation(builder.Services);
        _ = builder.Services.AddSingleton<ISecurityProfileSelector>(new TestSecurityProfileSelector());
        _ = builder.Services.AddKeyedSingleton<IAgentLoop>(AgentLoopComponentDefaults.LoopKeyValue, new RecordingAgentLoop());

        var exception = Should.Throw<AggregateException>(builder.Build);

        var compositionFailure = exception.InnerExceptions.OfType<AgentCompositionException>().ShouldHaveSingleItem();
        compositionFailure.Diagnostics.ShouldContain(static diagnostic => diagnostic.Code == "agentkit.catalog.empty");
        _ = exception.InnerExceptions.OfType<InvalidOperationException>().ShouldHaveSingleItem();
    }

    [Fact]
    public void Build_WhenSecurityGrantStoreIsDuplicated_RejectsInsteadOfUsingLastRegistration()
    {
        var storeFactoryCalls = 0;
        var builder = CompositionTestData.RunnableBuilder();
        _ = builder.Services.AddSingleton<ISecurityGrantStore>(_ =>
        {
            storeFactoryCalls++;
            throw new InvalidOperationException("Composition validation must not invoke a duplicate store factory.");
        });
        var exception = Should.Throw<AgentCompositionException>(builder.Build);
        exception.Diagnostics.ShouldContain(static diagnostic => diagnostic.Code == "agentkit.security-grant-store.ambiguous");
        storeFactoryCalls.ShouldBe(0);
    }

    [Fact]
    public void Build_WhenNoAgentIsPublished_ThrowsWithAnEmptyCatalogDiagnostic()
    {
        var builder = AgentEngine.CreateBuilder();
        CompositionTestData.AddRequiredSecurityGrantStore(builder.Services);
        CompositionTestData.AddHookKernelForEngineValidation(builder.Services);
        _ = builder.Services.AddSingleton<ISecurityProfileSelector>(new TestSecurityProfileSelector());
        _ = builder.Services.AddKeyedSingleton<IAgentLoop>(AgentLoopComponentDefaults.LoopKeyValue, new RecordingAgentLoop());
        var exception = Should.Throw<AgentCompositionException>(builder.Build);
        exception.Diagnostics.ShouldContain(diagnostic => diagnostic.Code == "agentkit.catalog.empty");
    }

    [Fact]
    public void Build_WhenNoLoopIsRegistered_ThrowsWithAnUnresolvableLoopDiagnostic()
    {
        var builder = AgentEngine.CreateBuilder();
        CompositionTestData.AddRequiredSecurityGrantStore(builder.Services);
        _ = builder.Services.AddAgent(CompositionTestData.Definition());
        var exception = Should.Throw<AgentCompositionException>(builder.Build);
        exception.Diagnostics.ShouldContain(diagnostic => diagnostic.Code == "agentkit.loop.unresolvable");
    }

    [Fact]
    public async Task Build_WhenLoopUsesFactory_DoesNotActivateLoopUntilRunStarts()
    {
        var factoryCalls = 0;
        var definition = CompositionTestData.Definition();
        var builder = AgentEngine.CreateBuilder();
        var sessions = new InMemoryTestSessionCoordinator();
        _ = builder.Services.AddSingleton<ISessionCoordinator>(sessions);
        CompositionTestData.AddRunProfiles(builder.Services, definition);
        CompositionTestData.AddRunServicesFakes(builder.Services);
        CompositionTestData.SeedSession(sessions, definition.Id, CompositionTestData.SessionId);
        _ = builder.Services.AddAgent(definition);
        _ = builder.Services.AddKeyedScoped<IAgentLoop>(AgentLoopComponentDefaults.LoopKeyValue, (_, _) =>
        {
            factoryCalls++;
            return new RecordingAgentLoop();
        });
        await using var engine = builder.Build();
        factoryCalls.ShouldBe(0);
        var agent = (await engine.GetAgentAsync(definition.Id, TestContext.Current.CancellationToken)).RequireResolved();
        _ = await agent.RunAsync<string>(CompositionTestData.SessionId, CompositionTestData.Identity(), CompositionTestData.Input(), options: CompositionTestData.RunOptions(), cancellationToken: TestContext.Current.CancellationToken);
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
        _ = builder.Services.AddKeyedScoped<IAgentLoop>(AgentLoopComponentDefaults.LoopKeyValue, (_, _) =>
        {
            factoryCalls++;
            return new RecordingAgentLoop();
        });
        _ = builder.Services.AddKeyedScoped<IAgentLoop>(AgentLoopComponentDefaults.LoopKeyValue, (_, _) =>
        {
            factoryCalls++;
            return new RecordingAgentLoop();
        });
        var exception = Should.Throw<AgentCompositionException>(builder.Build);
        exception.Diagnostics.ShouldContain(static diagnostic => diagnostic.Code == "agentkit.loop.ambiguous");
        factoryCalls.ShouldBe(0);
    }

    [Fact]
    public void Build_WhenLoopConstructorDependencyIsMissing_RejectsWithoutConstructingLoop()
    {
        MissingDependencyAgentLoop.ConstructorCalls = 0;
        var definition = CompositionTestData.Definition();
        var builder = AgentEngine.CreateBuilder();
        CompositionTestData.AddRunProfiles(builder.Services, definition);
        CompositionTestData.AddHookKernelForEngineValidation(builder.Services);
        _ = builder.Services.AddAgent(definition);
        _ = builder.Services.AddKeyedScoped<IAgentLoop, MissingDependencyAgentLoop>(AgentLoopComponentDefaults.LoopKeyValue);
        var exception = Should.Throw<AggregateException>(builder.Build);
        exception.ToString().ShouldContain(nameof(UnregisteredLoopDependency));
        MissingDependencyAgentLoop.ConstructorCalls.ShouldBe(0);
    }

    [Fact]
    public void Build_WhenLoopIsRegisteredUnderADifferentKeyThanTheDefinitionSelects_RejectsWithAMissingLoopDiagnostic()
    {
        var factoryCalls = 0;
        var definition = CompositionTestData.Definition();
        var builder = AgentEngine.CreateBuilder();
        CompositionTestData.AddRunProfiles(builder.Services, definition);
        CompositionTestData.AddHookKernelForEngineValidation(builder.Services);
        _ = builder.Services.AddAgent(definition);
        _ = builder.Services.AddKeyedScoped<IAgentLoop>("selected", (_, _) =>
        {
            factoryCalls++;
            return new RecordingAgentLoop();
        });
        var exception = Should.Throw<AgentCompositionException>(builder.Build);
        exception.Diagnostics.ShouldContain(static diagnostic => diagnostic.Code == "agentkit.definition.loop.missing");
        factoryCalls.ShouldBe(0);
    }

    [Fact]
    public void Build_WhenDefinitionExplicitlySelectsAnInputCoordinatorKeyWithNoMatchingRegistration_RejectsWithAnOptionalCollaboratorDiagnostic()
    {
        var definition = CompositionTestData.Definition() with { InputCoordinatorKey = new ComponentKey<IInputCoordinator>("missing") };
        var builder = CompositionTestData.RunnableBuilder(definition: definition);

        var exception = Should.Throw<AgentCompositionException>(builder.Build);

        exception.Diagnostics.ShouldContain(static diagnostic => diagnostic.Code == "agentkit.definition.optional-collaborator.missing");
    }

    [Fact]
    public void Build_WhenDefinitionExplicitlySelectsAnOutputPublisherKeyWithNoMatchingRegistration_RejectsWithAnOptionalCollaboratorDiagnostic()
    {
        var definition = CompositionTestData.Definition() with { OutputPublisherKey = new ComponentKey<IOutputPublisher>("missing") };
        var builder = CompositionTestData.RunnableBuilder(definition: definition);

        var exception = Should.Throw<AgentCompositionException>(builder.Build);

        exception.Diagnostics.ShouldContain(static diagnostic => diagnostic.Code == "agentkit.definition.optional-collaborator.missing");
    }

    [Fact]
    public async Task Build_WhenDefinitionDoesNotSelectAnInputCoordinatorOrOutputPublisherKey_AcceptsWithoutRequiringEither()
    {
        var builder = CompositionTestData.RunnableBuilder();

        await using var engine = builder.Build();

        _ = engine;
    }

    [Fact]
    public async Task Build_WhenDefinitionExplicitlySelectsAMatchingKeyedInputCoordinator_Accepts()
    {
        var key = new ComponentKey<IInputCoordinator>("matching");
        var definition = CompositionTestData.Definition() with { InputCoordinatorKey = key };
        var builder = CompositionTestData.RunnableBuilder(definition: definition);
        _ = builder.Services.AddKeyedSingleton<IInputCoordinator>(key.Value, static (_, _) => throw new InvalidOperationException("Unused test coordinator."));

        await using var engine = builder.Build();

        _ = engine;
    }

    [Fact]
    public async Task Build_WhenLoopHasAKeyedAlternativeUnderAnUnselectedKey_AcceptsWithoutActivatingTheAlternative()
    {
        var factoryCalls = 0;
        var builder = CompositionTestData.RunnableBuilder();
        _ = builder.Services.AddKeyedScoped<IAgentLoop>("unselected", (_, _) =>
        {
            factoryCalls++;
            throw new InvalidOperationException("An unselected keyed loop alternative must not be activated.");
        });
        await using var engine = builder.Build();
        _ = engine.ShouldNotBeNull();
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
        _ = services.AddKeyedScoped<IAgentLoop>(AgentLoopComponentDefaults.LoopKeyValue, (_, _) =>
        {
            factoryCalls++;
            return new RecordingAgentLoop();
        });
        CompositionTestData.AddRunServicesFakes(services);
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
        _ = builder.Services.AddKeyedSingleton<IAgentLoop>(AgentLoopComponentDefaults.LoopKeyValue, loop);
        CompositionTestData.AddRunServicesFakes(builder.Services);
        await using var engine = builder.Build();
        loop.DisposeCount.ShouldBe(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Engine_WhenDisposedAndThenUsed_ThrowsObjectDisposedExceptionOnBothOwnershipPaths(bool hostManaged)
    {
        // Agent.RunAsync documents ObjectDisposedException when the owning engine has been disposed.
        // A host-managed engine's DisposeAsync is intentionally a no-op over the host-owned provider
        // (the host owns disposal of its own container), but the engine must still stop admitting new
        // work after it has been told it is disposed, on both ownership paths.
        AgentEngine engine;
        AgentId agentId;
        if (hostManaged)
        {
            var services = CreateHostedServices();
            var provider = CompositionTestData.BuildHostedProvider(services);
            engine = provider.GetRequiredService<AgentEngine>();
            agentId = CompositionTestData.Definition().Id;
        }
        else
        {
            var builder = CompositionTestData.RunnableBuilder();
            engine = builder.Build();
            agentId = CompositionTestData.Definition().Id;
        }

        var agent = (await engine.GetAgentAsync(agentId, TestContext.Current.CancellationToken)).RequireResolved();
        await engine.DisposeAsync();

        _ = await Should.ThrowAsync<ObjectDisposedException>(
            () => engine.GetAgentsAsync(TestContext.Current.CancellationToken).AsTask());
        _ = await Should.ThrowAsync<ObjectDisposedException>(
            () => engine.GetAgentAsync(agentId, TestContext.Current.CancellationToken).AsTask());
        _ = await Should.ThrowAsync<ObjectDisposedException>(
            () => agent.RunAsync<string>(CompositionTestData.SessionId, CompositionTestData.Identity(), CompositionTestData.Input(), options: CompositionTestData.RunOptions(), cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Build_WhenAgentEngineIsResolvedFromItsOwnServices_ReturnsTheSameOwningEngine()
    {
        // AgentEngine.Services is a documented composition-root surface for the application that
        // built it. Resolving AgentEngine from that same provider must yield this exact owning
        // engine, not a second engine minted by AddAgentKit()'s host-managed factory
        // (ownedProvider: null) over the same container.
        var builder = CompositionTestData.RunnableBuilder();
        await using var engine = builder.Build();

        var resolved = engine.Services.GetRequiredService<AgentEngine>();

        resolved.ShouldBeSameAs(engine);
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
        _ = builder.Services.AddSingleton<TimeProvider>(_ => timeProvider = new TrackingTimeProvider());
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

        public Task<AgentLoopResult> RunAsync(AgentLoopRunRequest request, AgentRunServices services, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class DisposableAgentLoop: IAgentLoop, IDisposable
    {
        public int DisposeCount { get; private set; }

        public Task<AgentLoopResult> RunAsync(AgentLoopRunRequest request, AgentRunServices services, CancellationToken cancellationToken = default) => throw new NotSupportedException();
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Build_WhenLoggingInfrastructureIsExplicitlySelected_ValidatesEquallyAcrossOwnershipModes(bool hostManaged)
    {
        if (hostManaged)
        {
            var services = CreateHostedServices();
            AddInfrastructure(services);
            await using var provider = CompositionTestData.BuildHostedProvider(services);
            _ = provider.GetRequiredService<AgentEngine>().ShouldNotBeNull();
            return;
        }

        var builder = CompositionTestData.RunnableBuilder();
        AddInfrastructure(builder.Services);
        await using var engine = builder.Build();
        _ = engine.ShouldNotBeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Build_WhenSelectedInfrastructureIsOpaque_RejectsEquallyWithoutInvokingFactory(bool hostManaged)
    {
        var factoryCalls = 0;
        var standaloneBuilder = hostManaged ? null : CompositionTestData.RunnableBuilder();
        var services = hostManaged ? CreateHostedServices() : standaloneBuilder!.Services;
        _ = services.AddSingleton<IInfrastructureRoot, InfrastructureRoot>();
        _ = services.AddSingleton<IExternal>(_ =>
        {
            factoryCalls++;
            return new External();
        });
        _ = services.DeclareAgentKitComponent(RootRegistration(InfrastructureDependency<IExternal>()));
        var exception = Should.Throw<AgentCompositionException>(() =>
        {
            if (hostManaged)
            {
                _ = CompositionTestData.BuildHostedProvider(services);
            }
            else
            {
                _ = standaloneBuilder!.Build();
            }
        });
        exception.Diagnostics.Select(static diagnostic => diagnostic.Code).ShouldContain("agentkit.component-infrastructure.opaque-factory");
        factoryCalls.ShouldBe(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Build_WhenInfrastructureExceedsConfiguredBound_RejectsEquallyAcrossOwnershipModes(bool hostManaged)
    {
        var standaloneBuilder = hostManaged ? null : CompositionTestData.RunnableBuilder();
        var services = hostManaged ? CreateHostedServices() : standaloneBuilder!.Services;
        AddInfrastructure(services);
        var compositionOptions = new AgentKitCompositionOptions(maximumDerivedInfrastructureRegistrations: 1);
        _ = standaloneBuilder?.CompositionOptions = compositionOptions;
        var exception = Should.Throw<AgentCompositionException>(() =>
        {
            if (hostManaged)
            {
                var factory = new AgentKitServiceProviderFactory(compositionOptions, new ServiceProviderOptions());
                _ = factory.CreateServiceProvider(factory.CreateBuilder(services));
            }
            else
            {
                _ = standaloneBuilder!.Build();
            }
        });
        exception.Diagnostics.Select(static diagnostic => diagnostic.Code).ShouldContain("agentkit.component-infrastructure.validation-bound-exceeded");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Build_WhenLaterBuildUsesLargerBound_CapturesFreshProviderLocalEvidence(bool hostManaged)
    {
        var standaloneBuilder = hostManaged ? null : CompositionTestData.RunnableBuilder();
        var services = hostManaged ? CreateHostedServices() : standaloneBuilder!.Services;
        AddInfrastructure(services);
        var limitedOptions = new AgentKitCompositionOptions(1);
        _ = standaloneBuilder?.CompositionOptions = limitedOptions;
        _ = Should.Throw<AgentCompositionException>(() =>
        {
            if (hostManaged)
            {
                var factory = new AgentKitServiceProviderFactory(limitedOptions, new ServiceProviderOptions());
                _ = factory.CreateServiceProvider(factory.CreateBuilder(services));
            }
            else
            {
                _ = standaloneBuilder!.Build();
            }
        });
        if (hostManaged)
        {
            var factory = new AgentKitServiceProviderFactory();
            await using var provider = (ServiceProvider) factory.CreateServiceProvider(factory.CreateBuilder(services));
            _ = provider.GetRequiredService<AgentEngine>();
        }
        else
        {
            standaloneBuilder!.CompositionOptions = new AgentKitCompositionOptions();
            await using var engine = standaloneBuilder.Build();
            _ = engine.ShouldNotBeNull();
        }
    }

    private static ServiceCollection CreateHostedServices()
    {
        var definition = CompositionTestData.Definition();
        var services = new ServiceCollection();
        _ = services.AddAgentKit();
        _ = services.AddKeyedSingleton<IAgentLoop>(AgentLoopComponentDefaults.LoopKeyValue, new RecordingAgentLoop());
        CompositionTestData.AddRunProfiles(services, definition);
        _ = services.AddAgent(definition);
        CompositionTestData.AddRunServicesFakes(services);
        return services;
    }

    private static void AddInfrastructure(IServiceCollection services)
    {
        _ = services.AddLogging();
        _ = services.AddSingleton<IInfrastructureRoot, InfrastructureRoot>();
        _ = services.DeclareAgentKitComponent(RootRegistration(InfrastructureDependency<ILogger<LoggerCategory>>(ComponentDependencyCardinality.OptionalSingular)));
    }

    private static ComponentRegistrationDescriptor RootRegistration(params ComponentDependencyDescriptor[] dependencies) => new(ComponentContractReference.Unkeyed<IInfrastructureRoot>(), typeof(InfrastructureRoot), ServiceLifetime.Singleton, [.. dependencies]);
    private static ComponentDependencyDescriptor InfrastructureDependency<TService>(ComponentDependencyCardinality cardinality = ComponentDependencyCardinality.RequiredSingular)
        where TService : class => new(ComponentContractReference.Unkeyed<TService>(), cardinality, factoryBoundary: null, ComponentDependencyValidationBoundary.MicrosoftDependencyInjectionInfrastructure);
    private interface IInfrastructureRoot;
    private sealed class InfrastructureRoot: IInfrastructureRoot;
    private sealed class LoggerCategory;
    private interface IExternal;
    private sealed class External: IExternal;
    [Fact]
    public void Build_WhenConfiguredSourceHasNoBootstrapSnapshot_RejectsNotReadyWithoutReading()
    {
        ThrowingBootstrapTestSource.Reset();
        var builder = AgentEngine.CreateBuilder();
        CompositionTestData.AddRequiredSecurityGrantStore(builder.Services);
        CompositionTestData.AddHookKernelForEngineValidation(builder.Services);
        _ = builder.Services.AddSingleton<ISecurityProfileSelector>(new TestSecurityProfileSelector());
        _ = builder.Services.AddKeyedSingleton<IAgentLoop>(AgentLoopComponentDefaults.LoopKeyValue, new RecordingAgentLoop());
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
        _ = builder.Services.AddKeyedSingleton<IAgentLoop>(AgentLoopComponentDefaults.LoopKeyValue, new RecordingAgentLoop());
        _ = builder.Services.AddAgentDefinitionSource<ThrowingBootstrapTestSource>();
        _ = builder.Services.AddAgentDefinitionSnapshot(new AgentDefinitionSourceSnapshot(new AgentDefinitionSourceId("test-source"), new AgentDefinitionSourceVersion(1), 0, [definition]));
        CompositionTestData.AddRunProfiles(builder.Services, definition);
        CompositionTestData.AddRunServicesFakes(builder.Services);
        await using var engine = builder.Build();
        _ = engine.ShouldNotBeNull();
        ThrowingBootstrapTestSource.Reads.ShouldBe(0);
    }

    [Fact]
    public async Task Build_WhenAgentIsRegistered_UsesMaterializedBootstrapWithoutReadingSource()
    {
        var builder = AgentEngine.CreateBuilder();
        _ = builder.Services.AddKeyedSingleton<IAgentLoop>(AgentLoopComponentDefaults.LoopKeyValue, new RecordingAgentLoop());
        var definition = CompositionTestData.Definition();
        _ = builder.Services.AddAgent(definition);
        CompositionTestData.AddRunProfiles(builder.Services, definition);
        CompositionTestData.AddRunServicesFakes(builder.Services);
        await using var engine = builder.Build();
        _ = engine.ShouldNotBeNull();
    }

    [Fact]
    public void Build_WhenDeclaredGraphCycles_RejectsBeforeProviderOrApplicationFactoriesRun()
    {
        var applicationFactoryCalls = 0;
        var runIdFactoryCalls = 0;
        var builder = CompositionTestData.RunnableBuilder();
        _ = builder.Services.RemoveAll<IIdentifierGenerator<RunId>>();
        _ = builder.Services.AddSingleton<IIdentifierGenerator<RunId>>(_ =>
        {
            runIdFactoryCalls++;
            return new RunIdGenerator();
        });
        _ = builder.Services.AddSingleton<IFirst>(_ =>
        {
            applicationFactoryCalls++;
            return new First();
        });
        _ = builder.Services.AddSingleton<ISecond>(_ =>
        {
            applicationFactoryCalls++;
            return new Second();
        });
        _ = builder.Services.DeclareAgentKitComponent(Registration<IFirst, First>(ServiceLifetime.Singleton, ComponentContractReference.Unkeyed<ISecond>()));
        _ = builder.Services.DeclareAgentKitComponent(Registration<ISecond, Second>(ServiceLifetime.Singleton, ComponentContractReference.Unkeyed<IFirst>()));
        var exception = Should.Throw<AgentCompositionException>(builder.Build);
        exception.Diagnostics.ShouldContain(static diagnostic => diagnostic.Code == "agentkit.component-dependency.cycle");
        applicationFactoryCalls.ShouldBe(0);
        runIdFactoryCalls.ShouldBe(0);
    }

    [Fact]
    public void Build_WhenMetadataUsesFactory_RejectsBeforeMetadataFactoryRuns()
    {
        var metadataFactoryCalls = 0;
        var builder = CompositionTestData.RunnableBuilder();
        _ = builder.Services.AddSingleton(_ =>
        {
            metadataFactoryCalls++;
            return Registration<ILeaf, Leaf>(ServiceLifetime.Singleton);
        });
        var exception = Should.Throw<AgentCompositionException>(builder.Build);
        exception.Diagnostics.ShouldContain(static diagnostic => diagnostic.Code == "agentkit.component-registration.metadata-opaque");
        metadataFactoryCalls.ShouldBe(0);
    }

    [Fact]
    public async Task Build_WhenSameBuilderChangesBetweenBuilds_CapturesFreshEvidenceAndKeepsEarlierEnginePinned()
    {
        var builder = CompositionTestData.RunnableBuilder();
        await using var first = builder.Build();
        var firstServiceCount = first.ComponentRegistrations.Services.Length;
        _ = builder.Services.AddSingleton<ILeaf, Leaf>();
        _ = builder.Services.DeclareAgentKitComponent(Registration<ILeaf, Leaf>(ServiceLifetime.Scoped));
        var exception = Should.Throw<AgentCompositionException>(builder.Build);
        exception.Diagnostics.ShouldContain(static diagnostic => diagnostic.Code == "agentkit.component-registration.lifetime-mismatch");
        first.ComponentRegistrations.Registrations.ShouldBeEmpty();
        first.ComponentRegistrations.Services.Length.ShouldBe(firstServiceCount);
        builder.Services.Count.ShouldBeGreaterThan(firstServiceCount);
    }

    [Fact]
    public async Task Build_WhenNoMetadataIsPublished_RemainsExplicitlyPartial()
    {
        await using var engine = CompositionTestData.RunnableBuilder().Build();
        engine.ComponentRegistrations.RepresentsCompleteRunnableGraph.ShouldBeFalse();
        engine.ComponentRegistrations.UnrepresentedRequiredSpine.ShouldContain(ComponentContractReference.Unkeyed<IAgentLoop>());
    }

    private static ComponentRegistrationDescriptor Registration<TContract, TImplementation>(ServiceLifetime lifetime, params ComponentContractReference[] dependencies)
        where TContract : class where TImplementation : class, TContract => new(ComponentContractReference.Unkeyed<TContract>(), typeof(TImplementation), lifetime, [.. dependencies.Select(static dependency => new ComponentDependencyDescriptor(dependency, ComponentDependencyCardinality.RequiredSingular))]);
    private interface IFirst;
    private interface ISecond;
    private interface ILeaf;
    private sealed class First: IFirst;
    private sealed class Second: ISecond;
    private sealed class Leaf: ILeaf;
    private sealed class RunIdGenerator: IIdentifierGenerator<RunId>
    {
        public RunId Create() => new(Guid.NewGuid());
    }

    [Fact]
    public void Build_WhenDefinitionUsesLegacyUnconfiguredShape_RejectsAsUnrunnable()
    {
        var configured = CompositionTestData.Definition();
        var legacy = new AgentDefinition(configured.Id, configured.Revision, configured.DisplayName, configured.Models, configured.ModelRequirements, configured.Instructions, configured.Tools, configured.ToolChoice, configured.Settings, configured.RunDefaults, configured.Extensions);
        var builder = AgentEngine.CreateBuilder();
        CompositionTestData.AddRequiredSecurityGrantStore(builder.Services);
        CompositionTestData.AddHookKernelForEngineValidation(builder.Services);
        _ = builder.Services.AddAgent(legacy);
        _ = builder.Services.AddKeyedSingleton<IAgentLoop>(AgentLoopComponentDefaults.LoopKeyValue, new RecordingAgentLoop());
        _ = builder.Services.AddSingleton<ISecurityProfileSelector>(new TestSecurityProfileSelector());
        var exception = Should.Throw<AgentCompositionException>(builder.Build);
        exception.Diagnostics.ShouldContain(diagnostic => diagnostic.Code == "agentkit.definition.profiles.missing");
    }

    [Fact]
    public void Build_WhenRunProfileReaderIsNotReady_RejectsComposition()
    {
        var definition = CompositionTestData.Definition();
        var reader = new MutableRunProfilePublicationReader(currentSnapshot: null, new AgentRunProfilePublicationUnavailable("Not ready."));
        var builder = Builder(definition, reader);
        var exception = Should.Throw<AgentCompositionException>(builder.Build);
        exception.Diagnostics.ShouldContain(diagnostic => diagnostic.Code == "agentkit.run-profile.not-ready");
        reader.Reads.ShouldBe(0);
    }

    [Fact]
    public void Build_WhenExactRunProfileIsMissing_RejectsComposition()
    {
        var definition = CompositionTestData.Definition();
        var reader = new MutableRunProfilePublicationReader(new AgentRunProfilePublicationSnapshot([]), new AgentRunProfilePublicationUnavailable("Missing."));
        var builder = Builder(definition, reader);
        var exception = Should.Throw<AgentCompositionException>(builder.Build);
        exception.Diagnostics.ShouldContain(diagnostic => diagnostic.Code == "agentkit.run-profile.missing");
        reader.Reads.ShouldBe(0);
    }

    [Fact]
    public void Build_WhenDefinitionProfileKeysDifferFromPublication_RejectsComposition()
    {
        var definition = CompositionTestData.Definition();
        var publication = CompositionTestData.RunProfile(definition);
        var mismatched = new AgentRunProfilePublication(new SecurityProfilePublication(definition.Id, definition.Revision, publication.SecurityProfile.ConfigurationVersion, new SecurityProfileKey("different"), publication.SecurityProfile.ProfileVersion, publication.SecurityProfile.PolicySnapshot, publication.SecurityProfile.AuthorityKey), publication.SessionProfile);
        var reader = new MutableRunProfilePublicationReader(new AgentRunProfilePublicationSnapshot([mismatched]), new AgentRunProfilePublicationFound(mismatched));
        var builder = Builder(definition, reader);
        var exception = Should.Throw<AgentCompositionException>(builder.Build);
        exception.Diagnostics.ShouldContain(diagnostic => diagnostic.Code == "agentkit.run-profile.key-mismatch");
    }

    [Fact]
    public void Build_WhenExactRunProfileCoordinatesAreDuplicated_RejectsActivation()
    {
        var definition = CompositionTestData.Definition();
        var builder = CompositionTestData.RunnableBuilder(definition: definition);
        _ = builder.Services.AddAgentRunProfilePublication(CompositionTestData.RunProfile(definition));
        var exception = Should.Throw<ArgumentException>(builder.Build);
        exception.ParamName.ShouldBe("publications");
    }

    [Fact]
    public void Build_WhenCustomReaderSnapshotContainsDuplicateCoordinatesWithoutItsOwnGuard_RejectsComposition()
    {
        // AgentRunProfilePublicationSnapshot itself performs no duplicate check, and the default reader
        // guards against duplicates in its own constructor. This exercises AgentCompositionValidator's own
        // defensive duplicate detection, which only a non-default IAgentRunProfilePublicationReader
        // implementation without that same guard can ever reach.
        var definition = CompositionTestData.Definition();
        var first = CompositionTestData.RunProfile(definition);
        var second = CompositionTestData.RunProfile(definition);
        var reader = new MutableRunProfilePublicationReader(
            new AgentRunProfilePublicationSnapshot([first, second]),
            new AgentRunProfilePublicationFound(first));
        var builder = Builder(definition, reader);

        var exception = Should.Throw<AgentCompositionException>(builder.Build);

        exception.Diagnostics.ShouldContain(diagnostic => diagnostic.Code == "agentkit.run-profile.duplicate");
    }

    [Fact]
    public async Task Build_WhenReaderAlternatesReadiness_PinsOnlyTheSnapshotActuallyValidated()
    {
        var definition = CompositionTestData.Definition();
        var validated = CompositionTestData.RunProfile(definition);
        var replacement = new AgentRunProfilePublication(new SecurityProfilePublication(definition.Id, definition.Revision, new ConfigurationVersion(2), validated.SecurityProfile.ProfileKey, validated.SecurityProfile.ProfileVersion, validated.SecurityProfile.PolicySnapshot, validated.SecurityProfile.AuthorityKey), validated.SessionProfile);
        var reader = new AlternatingRunProfilePublicationReader(new AgentRunProfilePublicationSnapshot([validated]), new AgentRunProfilePublicationSnapshot([replacement]), new AgentRunProfilePublicationFound(replacement));
        var runIds = new CountingRunIdGenerator();
        var builder = AgentEngine.CreateBuilder();
        CompositionTestData.AddRequiredSecurityGrantStore(builder.Services);
        _ = builder.Services.AddAgent(definition);
        _ = builder.Services.AddKeyedSingleton<IAgentLoop>(AgentLoopComponentDefaults.LoopKeyValue, new RecordingAgentLoop());
        _ = builder.Services.AddSingleton<ISecurityProfileSelector>(new TestSecurityProfileSelector());
        _ = builder.Services.Replace(ServiceDescriptor.Singleton<IAgentRunProfilePublicationReader>(reader));
        _ = builder.Services.Replace(ServiceDescriptor.Singleton<IIdentifierGenerator<RunId>>(runIds));
        CompositionTestData.AddRunServicesFakes(builder.Services);
        await using var engine = builder.Build();
        var agent = (await engine.GetAgentAsync(definition.Id, TestContext.Current.CancellationToken)).RequireResolved();
        _ = (await agent.RunAsync<string>(CompositionTestData.SessionId, CompositionTestData.Identity(), CompositionTestData.Input(), options: CompositionTestData.RunOptions(), cancellationToken: TestContext.Current.CancellationToken)).ShouldBeOfType<AgentRunRejected<string>>();
        reader.SnapshotReads.ShouldBe(1);
        runIds.Created.ShouldBe(0);
    }

    private static AgentEngineBuilder Builder(AgentDefinition definition, IAgentRunProfilePublicationReader reader)
    {
        var builder = AgentEngine.CreateBuilder();
        CompositionTestData.AddRequiredSecurityGrantStore(builder.Services);
        CompositionTestData.AddHookKernelForEngineValidation(builder.Services);
        _ = builder.Services.AddAgent(definition);
        _ = builder.Services.AddKeyedSingleton<IAgentLoop>(AgentLoopComponentDefaults.LoopKeyValue, new RecordingAgentLoop());
        _ = builder.Services.AddSingleton<ISecurityProfileSelector>(new TestSecurityProfileSelector());
        _ = builder.Services.Replace(ServiceDescriptor.Singleton(reader));
        return builder;
    }

    public static TheoryData<Type, string> RequiredServices => new()
    {
        {
            typeof(AgentEngine),
            "agentkit.engine"
        },
        {
            typeof(IAgentDefinitionCatalog),
            "agentkit.catalog"
        },
        {
            typeof(IAgentRunProfilePublicationReader),
            "agentkit.run-profile-reader"
        },
        {
            typeof(ISecurityProfileSelector),
            "agentkit.security-profile-selector"
        },
        {
            typeof(ISecurityGrantStore),
            "agentkit.security-grant-store"
        },
        {
            typeof(TimeProvider),
            "agentkit.time"
        },
        {
            typeof(IIdentifierGenerator<RunId>),
            "agentkit.runid"
        },
        {
            typeof(IIdentifierGenerator<OperationId>),
            "agentkit.operationid"
        },

        // IAgentLoop is deliberately excluded: it is keyed and scoped rather than singular and unkeyed, so its
        // duplicate/keyed-alternative semantics are covered by its own dedicated tests above instead of this
        // shared "singular unkeyed service" theory.
    };

    [Theory]
    [MemberData(nameof(RequiredServices))]
    public void Build_WhenSingularServiceIsDuplicated_RejectsBeforeAnyApplicationFactory(Type serviceType, string code)
    {
        // Arrange
        var builder = CompositionTestData.RunnableBuilder();
        var factoryCalls = 0;
        _ = builder.Services.AddSingleton(serviceType, _ =>
        {
            factoryCalls++;
            throw new InvalidOperationException("Duplicate service factories must never run.");
        });
        _ = builder.Services.Replace(ServiceDescriptor.Singleton<IAgentDefinitionCatalog>(_ =>
        {
            factoryCalls++;
            throw new InvalidOperationException("Catalog activation must follow cardinality validation.");
        }));
        // Act
        var exception = Should.Throw<AgentCompositionException>(builder.Build);
        // Assert
        exception.Diagnostics.ShouldContain(diagnostic => diagnostic.Code == $"{code}.ambiguous");
        factoryCalls.ShouldBe(0);
    }

    [Theory]
    [MemberData(nameof(RequiredServices))]
    public async Task Build_WhenSingularServiceHasKeyedAlternatives_AcceptsWithoutActivatingAlternatives(Type serviceType, string code)
    {
        // Arrange
        _ = code;
        var builder = CompositionTestData.RunnableBuilder();
        var factoryCalls = 0;
        _ = builder.Services.AddKeyedSingleton(serviceType, "separate", (_, _) =>
        {
            factoryCalls++;
            throw new InvalidOperationException("Unselected keyed alternatives must not run.");
        });
        // Act
        await using var engine = builder.Build();
        // Assert
        factoryCalls.ShouldBe(0);
    }

    [Fact]
    public void Build_WhenFacadeRegistrationIsRemoved_RejectsBeforeCatalogActivation()
    {
        var builder = CompositionTestData.RunnableBuilder();
        _ = builder.Services.RemoveAll<AgentEngine>();
        var factoryCalls = 0;
        _ = builder.Services.Replace(ServiceDescriptor.Singleton<IAgentDefinitionCatalog>(_ =>
        {
            factoryCalls++;
            throw new InvalidOperationException("A missing facade must fail before activation.");
        }));
        var exception = Should.Throw<AgentCompositionException>(builder.Build);
        exception.Diagnostics.ShouldContain(static diagnostic => diagnostic.Code == "agentkit.engine.missing");
        factoryCalls.ShouldBe(0);
    }
}
