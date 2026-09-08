// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

public sealed class ComponentRegistrationCompositionTests
{
    [Fact]
    public void ValidateComponentRegistrations_WhenSnapshotIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() =>
            AgentCompositionValidator.ValidateComponentRegistrations(null!));

        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("snapshot");
    }

    [Fact]
    public void ValidateComponentRegistrations_WhenFacadeGrantStoreIsMissing_ReportsMissingWithoutFactoryEffects()
    {
        var applicationFactoryCalls = 0;
        var services = new ServiceCollection();
        _ = services.AddAgentKit();
        _ = services.AddSingleton<ILeaf>(
            _ =>
            {
                applicationFactoryCalls++;
                return new Leaf();
            });
        var snapshot = ComponentRegistrationSnapshot.Capture(services);

        var exception = Should.Throw<AgentCompositionException>(() =>
            AgentCompositionValidator.ValidateComponentRegistrations(snapshot));

        exception.Diagnostics.ShouldContain(
            static diagnostic => diagnostic.Code == "agentkit.security-grant-store.missing");
        applicationFactoryCalls.ShouldBe(0);
    }

    [Fact]
    public void ValidateComponentRegistrations_WhenFacadeGrantStoreIsAmbiguous_ReportsAmbiguousWithoutStoreFactoryEffects()
    {
        var storeFactoryCalls = 0;
        var services = new ServiceCollection();
        _ = services.AddAgentKit();
        _ = services.AddSingleton<ISecurityGrantStore>(
            _ =>
            {
                storeFactoryCalls++;
                throw new InvalidOperationException("Composition validation must not invoke the first store factory.");
            });
        _ = services.AddSingleton<ISecurityGrantStore>(
            _ =>
            {
                storeFactoryCalls++;
                throw new InvalidOperationException("Composition validation must not invoke the second store factory.");
            });
        var snapshot = ComponentRegistrationSnapshot.Capture(services);

        var exception = Should.Throw<AgentCompositionException>(() =>
            AgentCompositionValidator.ValidateComponentRegistrations(snapshot));

        exception.Diagnostics.ShouldContain(
            static diagnostic => diagnostic.Code == "agentkit.security-grant-store.ambiguous");
        storeFactoryCalls.ShouldBe(0);
    }

    [Fact]
    public void ValidateComponentRegistrations_WhenFacadeHasOneUnkeyedGrantStoreAndKeyedExtras_AcceptsWithoutCallbacks()
    {
        var storeFactoryCalls = 0;
        var applicationKey = new ThrowingServiceKey();
        var services = new ServiceCollection();
        _ = services.AddAgentKit();
        _ = services.AddSingleton<ISecurityGrantStore>(
            _ =>
            {
                storeFactoryCalls++;
                throw new InvalidOperationException("Composition validation must not invoke the store factory.");
            });
        _ = services.AddKeyedSingleton<ISecurityGrantStore>(
            applicationKey,
            (_, _) =>
            {
                storeFactoryCalls++;
                throw new InvalidOperationException("Composition validation must not invoke the keyed store factory.");
            });
        var snapshot = ComponentRegistrationSnapshot.Capture(services);

        Should.NotThrow(() => AgentCompositionValidator.ValidateComponentRegistrations(snapshot));

        storeFactoryCalls.ShouldBe(0);
        applicationKey.EqualsCalls.ShouldBe(0);
    }

    [Fact]
    public void ValidateComponentRegistrations_WhenGrantStoreUsesNullKeyedRegistration_TreatsActualDiDescriptorAsUnkeyed()
    {
        var grantStore = new StubSecurityGrantStore();
        var services = new ServiceCollection();
        _ = services.AddAgentKit();
        _ = services.AddKeyedSingleton<ISecurityGrantStore>(null, grantStore);
        var snapshot = ComponentRegistrationSnapshot.Capture(services);
        var store = snapshot.Services
            .Where(static descriptor => descriptor.ServiceType == typeof(ISecurityGrantStore))
            .ShouldHaveSingleItem();

        store.IsKeyedService.ShouldBeFalse();
        Should.NotThrow(() => AgentCompositionValidator.ValidateComponentRegistrations(snapshot));
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<ISecurityGrantStore>().ShouldBeSameAs(grantStore);
        provider.GetRequiredKeyedService<ISecurityGrantStore>(null).ShouldBeSameAs(grantStore);
    }

    [Fact]
    public void ValidateComponentRegistrations_WhenOnlyNonNullKeyedGrantStoreExists_RemainsMissingWithoutKeyCallbacks()
    {
        var applicationKey = new ThrowingServiceKey();
        var services = new ServiceCollection();
        _ = services.AddAgentKit();
        _ = services.AddKeyedSingleton<ISecurityGrantStore>(
            applicationKey,
            static (_, _) => throw new InvalidOperationException(
                "Composition validation must not invoke the keyed store factory."));
        var snapshot = ComponentRegistrationSnapshot.Capture(services);

        var exception = Should.Throw<AgentCompositionException>(() =>
            AgentCompositionValidator.ValidateComponentRegistrations(snapshot));

        exception.Diagnostics.ShouldContain(
            static diagnostic => diagnostic.Code == "agentkit.security-grant-store.missing");
        applicationKey.EqualsCalls.ShouldBe(0);
    }

    [Fact]
    public void Build_WhenDeclaredGraphCycles_RejectsBeforeProviderOrApplicationFactoriesRun()
    {
        var applicationFactoryCalls = 0;
        var runIdFactoryCalls = 0;
        var builder = CompositionTestData.RunnableBuilder();
        _ = builder.Services.RemoveAll<IIdentifierGenerator<RunId>>();
        _ = builder.Services.AddSingleton<IIdentifierGenerator<RunId>>(
            _ =>
            {
                runIdFactoryCalls++;
                return new RunIdGenerator();
            });
        _ = builder.Services.AddSingleton<IFirst>(
            _ =>
            {
                applicationFactoryCalls++;
                return new First();
            });
        _ = builder.Services.AddSingleton<ISecond>(
            _ =>
            {
                applicationFactoryCalls++;
                return new Second();
            });
        _ = builder.Services.DeclareAgentKitComponent(Registration<IFirst, First>(
            ServiceLifetime.Singleton, ComponentContractReference.Unkeyed<ISecond>()));
        _ = builder.Services.DeclareAgentKitComponent(Registration<ISecond, Second>(
            ServiceLifetime.Singleton, ComponentContractReference.Unkeyed<IFirst>()));

        var exception = Should.Throw<AgentCompositionException>(builder.Build);

        exception.Diagnostics.ShouldContain(
            static diagnostic => diagnostic.Code == "agentkit.component-dependency.cycle");
        applicationFactoryCalls.ShouldBe(0);
        runIdFactoryCalls.ShouldBe(0);
    }

    [Fact]
    public void Build_WhenMetadataUsesFactory_RejectsBeforeMetadataFactoryRuns()
    {
        var metadataFactoryCalls = 0;
        var builder = CompositionTestData.RunnableBuilder();
        _ = builder.Services.AddSingleton(
            _ =>
            {
                metadataFactoryCalls++;
                return Registration<ILeaf, Leaf>(ServiceLifetime.Singleton);
            });

        var exception = Should.Throw<AgentCompositionException>(builder.Build);

        exception.Diagnostics.ShouldContain(
            static diagnostic => diagnostic.Code == "agentkit.component-registration.metadata-opaque");
        metadataFactoryCalls.ShouldBe(0);
    }

    [Fact]
    public async Task Build_WhenSameBuilderChangesBetweenBuilds_CapturesFreshEvidenceAndKeepsEarlierEnginePinned()
    {
        var builder = CompositionTestData.RunnableBuilder();
        await using var first = builder.Build();
        var firstServiceCount = first.ComponentRegistrations.Services.Length;
        _ = builder.Services.AddSingleton<ILeaf, Leaf>();
        _ = builder.Services.DeclareAgentKitComponent(
            Registration<ILeaf, Leaf>(ServiceLifetime.Scoped));

        var exception = Should.Throw<AgentCompositionException>(builder.Build);

        exception.Diagnostics.ShouldContain(
            static diagnostic => diagnostic.Code == "agentkit.component-registration.lifetime-mismatch");
        first.ComponentRegistrations.Registrations.ShouldBeEmpty();
        first.ComponentRegistrations.Services.Length.ShouldBe(firstServiceCount);
        builder.Services.Count.ShouldBeGreaterThan(firstServiceCount);
    }

    [Fact]
    public async Task HostedAndStandalone_WhenRegistrationsMatch_CaptureEquivalentReadinessEvidence()
    {
        var registration = Registration<ILeaf, Leaf>(ServiceLifetime.Singleton);
        var standaloneBuilder = CompositionTestData.RunnableBuilder();
        _ = standaloneBuilder.Services.AddSingleton<ILeaf, Leaf>();
        _ = standaloneBuilder.Services.DeclareAgentKitComponent(registration);
        await using var standalone = standaloneBuilder.Build();

        var hostedServices = new ServiceCollection();
        ConfigureRunnable(hostedServices);
        _ = hostedServices.AddSingleton<ILeaf, Leaf>();
        _ = hostedServices.DeclareAgentKitComponent(registration);
        await using var provider = CompositionTestData.BuildHostedProvider(hostedServices);
        var hosted = provider.GetRequiredService<AgentEngine>();

        hosted.ComponentRegistrations.Registrations.ShouldBe(standalone.ComponentRegistrations.Registrations);
        hosted.ComponentRegistrations.UnrepresentedRequiredSpine.ShouldBe(
            standalone.ComponentRegistrations.UnrepresentedRequiredSpine);
        hosted.ComponentRegistrations.RepresentsCompleteRunnableGraph.ShouldBeFalse();
        standalone.ComponentRegistrations.RepresentsCompleteRunnableGraph.ShouldBeFalse();
    }

    [Fact]
    public async Task HostedProviders_WhenCollectionChanges_CapturePerProviderWithoutChangingEarlierEngine()
    {
        var services = new ServiceCollection();
        ConfigureRunnable(services);
        await using var firstProvider = CompositionTestData.BuildHostedProvider(services);
        var first = firstProvider.GetRequiredService<AgentEngine>();
        var firstServiceCount = first.ComponentRegistrations.Services.Length;
        _ = services.AddSingleton<ILeaf, Leaf>();
        _ = services.DeclareAgentKitComponent(Registration<ILeaf, Leaf>(ServiceLifetime.Scoped));
        var exception = Should.Throw<AgentCompositionException>(() =>
            CompositionTestData.BuildHostedProvider(services));

        exception.Diagnostics.ShouldContain(
            static diagnostic => diagnostic.Code == "agentkit.component-registration.lifetime-mismatch");
        first.ComponentRegistrations.Registrations.ShouldBeEmpty();
        first.ComponentRegistrations.Services.Length.ShouldBe(firstServiceCount);
    }

    [Fact]
    public async Task HostedProvider_WhenCollectionChangesBeforeDelayedEngineResolution_RetainsItsBuiltGraph()
    {
        var services = new ServiceCollection();
        ConfigureRunnable(services);
        await using var firstProvider = CompositionTestData.BuildHostedProvider(services);
        _ = services.AddSingleton<ILeaf, Leaf>();
        _ = services.DeclareAgentKitComponent(Registration<ILeaf, Leaf>(ServiceLifetime.Singleton));

        var first = firstProvider.GetRequiredService<AgentEngine>();
        await using var secondProvider = CompositionTestData.BuildHostedProvider(services);
        var second = secondProvider.GetRequiredService<AgentEngine>();

        first.ComponentRegistrations.Registrations.ShouldBeEmpty();
        first.ComponentRegistrations.Services.Count(static descriptor => descriptor.ServiceType == typeof(ILeaf))
            .ShouldBe(0);
        _ = second.ComponentRegistrations.Registrations.ShouldHaveSingleItem();
        second.ComponentRegistrations.Services.Count(static descriptor => descriptor.ServiceType == typeof(ILeaf))
            .ShouldBe(1);
        second.ComponentRegistrations.ShouldNotBeSameAs(first.ComponentRegistrations);
        first.ComponentRegistrations.Services.Count(
            static descriptor => descriptor.ServiceType == typeof(ComponentRegistrationSnapshot)).ShouldBe(1);
        second.ComponentRegistrations.Services.Count(
            static descriptor => descriptor.ServiceType == typeof(ComponentRegistrationSnapshot)).ShouldBe(1);
    }

    [Fact]
    public void HostedProvider_WhenBuiltWithoutAgentKitFactory_FailsClosedAtEngineResolution()
    {
        var services = new ServiceCollection();
        ConfigureRunnable(services);
        using var provider = services.BuildServiceProvider();

        var exception = Should.Throw<AgentCompositionException>(provider.GetRequiredService<AgentEngine>);

        exception.Diagnostics.ShouldContain(
            static diagnostic => diagnostic.Code == "agentkit.component-registration.snapshot-missing");
    }

    [Fact]
    public async Task Build_WhenNoMetadataIsPublished_RemainsExplicitlyPartial()
    {
        await using var engine = CompositionTestData.RunnableBuilder().Build();

        engine.ComponentRegistrations.RepresentsCompleteRunnableGraph.ShouldBeFalse();
        engine.ComponentRegistrations.UnrepresentedRequiredSpine.ShouldContain(
            ComponentContractReference.Unkeyed<IAgentLoop>());
    }

    private static void ConfigureRunnable(IServiceCollection services)
    {
        _ = services.AddAgentKit();
        _ = services.AddSingleton<IAgentLoop>(new RecordingAgentLoop());
        var definition = CompositionTestData.Definition();
        _ = services.AddAgent(definition);
        CompositionTestData.AddRunProfiles(services, definition);
    }

    private static ComponentRegistrationDescriptor Registration<TContract, TImplementation>(
        ServiceLifetime lifetime,
        params ComponentContractReference[] dependencies)
        where TContract : class
        where TImplementation : class, TContract => new(
            ComponentContractReference.Unkeyed<TContract>(),
            typeof(TImplementation),
            lifetime,
            [.. dependencies.Select(static dependency => new ComponentDependencyDescriptor(
                dependency, ComponentDependencyCardinality.RequiredSingular))]);

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

    private sealed class StubSecurityGrantStore: ISecurityGrantStore
    {
        public ValueTask RegisterAsync(
            SecurityGrant grant,
            CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(
            SecurityGrant grant,
            SecurityEnforcementRequest enforcement,
            CancellationToken cancellationToken = default) => ValueTask.FromResult(
                new GrantConsumptionResult(GrantConsumptionStatus.Unknown, 0, "Unused test store."));

        public ValueTask<bool> RevokeAsync(
            GrantId grantId,
            CancellationToken cancellationToken = default) => ValueTask.FromResult(false);
    }

    private sealed class ThrowingServiceKey
    {
        public int EqualsCalls { get; private set; }

        public override bool Equals(object? obj)
        {
            EqualsCalls++;
            throw new InvalidOperationException("Composition validation must not compare application service keys.");
        }

        public override int GetHashCode() => throw new InvalidOperationException(
            "Composition validation must not hash application service keys.");
    }
}
