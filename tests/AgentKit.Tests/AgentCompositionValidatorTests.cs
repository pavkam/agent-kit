// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;



/// <summary>Verifies AgentCompositionValidator behavior and contracts.</summary>
public sealed class AgentCompositionValidatorTests
{
    [Fact]
    public void ValidateComponentRegistrations_WhenSnapshotIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => AgentCompositionValidator.ValidateComponentRegistrations(null!));
        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("snapshot");
    }

    [Fact]
    public void ValidateComponentRegistrations_WhenFacadeGrantStoreIsMissing_ReportsMissingWithoutFactoryEffects()
    {
        var applicationFactoryCalls = 0;
        var services = new ServiceCollection();
        _ = services.AddAgentKit();
        _ = services.AddSingleton<ILeaf>(_ =>
        {
            applicationFactoryCalls++;
            return new Leaf();
        });
        var snapshot = ComponentRegistrationSnapshot.Capture(services);
        var exception = Should.Throw<AgentCompositionException>(() => AgentCompositionValidator.ValidateComponentRegistrations(snapshot));
        exception.Diagnostics.ShouldContain(static diagnostic => diagnostic.Code == "agentkit.security-grant-store.missing");
        applicationFactoryCalls.ShouldBe(0);
    }

    [Fact]
    public void ValidateComponentRegistrations_WhenFacadeGrantStoreIsAmbiguous_ReportsAmbiguousWithoutStoreFactoryEffects()
    {
        var storeFactoryCalls = 0;
        var services = new ServiceCollection();
        _ = services.AddAgentKit();
        _ = services.AddSingleton<ISecurityGrantStore>(_ =>
        {
            storeFactoryCalls++;
            throw new InvalidOperationException("Composition validation must not invoke the first store factory.");
        });
        _ = services.AddSingleton<ISecurityGrantStore>(_ =>
        {
            storeFactoryCalls++;
            throw new InvalidOperationException("Composition validation must not invoke the second store factory.");
        });
        var snapshot = ComponentRegistrationSnapshot.Capture(services);
        var exception = Should.Throw<AgentCompositionException>(() => AgentCompositionValidator.ValidateComponentRegistrations(snapshot));
        exception.Diagnostics.ShouldContain(static diagnostic => diagnostic.Code == "agentkit.security-grant-store.ambiguous");
        storeFactoryCalls.ShouldBe(0);
    }

    [Fact]
    public void ValidateComponentRegistrations_WhenFacadeHasOneUnkeyedGrantStoreAndKeyedExtras_AcceptsWithoutCallbacks()
    {
        var storeFactoryCalls = 0;
        var applicationKey = new ThrowingServiceKey();
        var services = new ServiceCollection();
        _ = services.AddAgentKit();
        _ = services.AddSingleton<ISecurityProfileSelector>(new TestSecurityProfileSelector());
        _ = services.AddKeyedSingleton<IAgentLoop>(AgentLoopComponentDefaults.LoopKeyValue, new RecordingAgentLoop());
        HookCompositionTestSupport.TryAddDefaultHookKernel(services);
        _ = services.AddSingleton<ISecurityGrantStore>(_ =>
        {
            storeFactoryCalls++;
            throw new InvalidOperationException("Composition validation must not invoke the store factory.");
        });
        _ = services.AddKeyedSingleton<ISecurityGrantStore>(applicationKey, (_, _) =>
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
        _ = services.AddSingleton<ISecurityProfileSelector>(new TestSecurityProfileSelector());
        _ = services.AddKeyedSingleton<IAgentLoop>(AgentLoopComponentDefaults.LoopKeyValue, new RecordingAgentLoop());
        HookCompositionTestSupport.TryAddDefaultHookKernel(services);
        _ = services.AddKeyedSingleton<ISecurityGrantStore>(null, grantStore);
        var snapshot = ComponentRegistrationSnapshot.Capture(services);
        var store = snapshot.Services.Where(static descriptor => descriptor.ServiceType == typeof(ISecurityGrantStore)).ShouldHaveSingleItem();
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
        _ = services.AddKeyedSingleton<ISecurityGrantStore>(applicationKey, static (_, _) => throw new InvalidOperationException("Composition validation must not invoke the keyed store factory."));
        var snapshot = ComponentRegistrationSnapshot.Capture(services);
        var exception = Should.Throw<AgentCompositionException>(() => AgentCompositionValidator.ValidateComponentRegistrations(snapshot));
        exception.Diagnostics.ShouldContain(static diagnostic => diagnostic.Code == "agentkit.security-grant-store.missing");
        applicationKey.EqualsCalls.ShouldBe(0);
    }

    private interface ILeaf;
    private sealed class Leaf: ILeaf;
    private sealed class StubSecurityGrantStore: ISecurityGrantStore
    {
        public ValueTask RegisterAsync(SecurityGrant grant, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(SecurityGrant grant, SecurityEnforcementRequest enforcement, CancellationToken cancellationToken = default) => ValueTask.FromResult(new GrantConsumptionResult(GrantConsumptionStatus.Unknown, 0, "Unused test store."));
        public ValueTask<bool> RevokeAsync(GrantId grantId, CancellationToken cancellationToken = default) => ValueTask.FromResult(false);
    }

    private sealed class ThrowingServiceKey
    {
        public int EqualsCalls { get; private set; }

        public override bool Equals(object? obj)
        {
            EqualsCalls++;
            throw new InvalidOperationException("Composition validation must not compare application service keys.");
        }

        public override int GetHashCode() => throw new InvalidOperationException("Composition validation must not hash application service keys.");
    }

    [Theory]
    [InlineData(typeof(IAgentDefinitionCatalog), "agentkit.catalog")]
    [InlineData(typeof(IAgentRunProfilePublicationReader), "agentkit.run-profile-reader")]
    [InlineData(typeof(ISecurityProfileSelector), "agentkit.security-profile-selector")]
    [InlineData(typeof(ISecurityGrantStore), "agentkit.security-grant-store")]
    [InlineData(typeof(TimeProvider), "agentkit.time")]
    [InlineData(typeof(IIdentifierGenerator<RunId>), "agentkit.runid")]
    [InlineData(typeof(IIdentifierGenerator<OperationId>), "agentkit.operationid")]
    public void ValidateComponentRegistrations_WhenOnlyKeyedServiceRemains_ReportsMissingWithoutFactories(Type serviceType, string code)
    {
        // Arrange
        var builder = CompositionTestData.RunnableBuilder();
        _ = builder.Services.RemoveAll(serviceType);
        var factoryCalls = 0;
        _ = builder.Services.AddKeyedSingleton(serviceType, "separate", (_, _) =>
        {
            factoryCalls++;
            throw new InvalidOperationException("A keyed service cannot satisfy an unkeyed requirement.");
        });
        // Act
        var exception = Should.Throw<AgentCompositionException>(() => AgentCompositionValidator.ValidateComponentRegistrations(ComponentRegistrationSnapshot.Capture(builder.Services)));
        // Assert
        exception.Diagnostics.ShouldContain(diagnostic => diagnostic.Code == $"{code}.missing");
        factoryCalls.ShouldBe(0);
    }

    [Fact]
    public void ValidateComponentRegistrations_WhenLoopIsOnlyRegisteredUnderAnUnselectedKey_AcceptsTheNarrowFacadeCheck()
    {
        // ValidateComponentRegistrations alone (unlike the full AgentCompositionValidator.Validate used at engine
        // build time) does not know which key an agent definition selects, so it only proves that some keyed
        // IAgentLoop registration exists. Whether it is the exact key a definition selects is proven separately
        // by ValidateCatalog, exercised end-to-end by
        // AgentEngineBuilderTests.Build_WhenLoopIsRegisteredUnderADifferentKeyThanTheDefinitionSelects_RejectsWithAMissingLoopDiagnostic.
        var builder = CompositionTestData.RunnableBuilder();
        _ = builder.Services.RemoveAll<IAgentLoop>();
        var factoryCalls = 0;
        _ = builder.Services.AddKeyedSingleton<IAgentLoop>("separate", (_, _) =>
        {
            factoryCalls++;
            throw new InvalidOperationException("An unselected keyed loop alternative must not be activated.");
        });

        Should.NotThrow(() => AgentCompositionValidator.ValidateComponentRegistrations(ComponentRegistrationSnapshot.Capture(builder.Services)));

        factoryCalls.ShouldBe(0);
    }

    [Fact]
    public void ValidateComponentRegistrations_WhenSeveralServicesAreMissing_ReportsAllBeforeActivation()
    {
        // Arrange
        var builder = CompositionTestData.RunnableBuilder();
        _ = builder.Services.RemoveAll<IAgentDefinitionCatalog>();
        _ = builder.Services.RemoveAll<TimeProvider>();
        _ = builder.Services.RemoveAll<ISecurityProfileSelector>();
        // Act
        var exception = Should.Throw<AgentCompositionException>(() => AgentCompositionValidator.ValidateComponentRegistrations(ComponentRegistrationSnapshot.Capture(builder.Services)));
        // Assert
        exception.Diagnostics.Select(static diagnostic => diagnostic.Code).ShouldBe(["agentkit.catalog.missing", "agentkit.security-profile-selector.missing", "agentkit.time.missing"]);
    }

    [Fact]
    public void ValidateComponentRegistrations_WhenHookKernelIsMissing_ReportsHookDispatcherMissingWithoutFactories()
    {
        var services = new ServiceCollection();
        _ = services.AddAgentKit();
        _ = services.AddSingleton<ISecurityProfileSelector>(new TestSecurityProfileSelector());
        _ = services.AddKeyedSingleton<IAgentLoop>(AgentLoopComponentDefaults.LoopKeyValue, new RecordingAgentLoop());
        var snapshot = ComponentRegistrationSnapshot.Capture(services);
        var exception = Should.Throw<AgentCompositionException>(() => AgentCompositionValidator.ValidateComponentRegistrations(snapshot));
        exception.Diagnostics.ShouldContain(static diagnostic => diagnostic.Code == "agentkit.hook-dispatcher.missing");
        exception.Diagnostics.ShouldContain(static diagnostic => diagnostic.Code == "agentkit.hook-catalog.missing");
    }

    [Fact]
    public void Build_WhenHookKernelWasNeverRegistered_RejectsWithHookDispatcherMissing()
    {
        var builder = CompositionTestData.RunnableBuilder();
        _ = builder.Services.RemoveAll<IHookDispatcher>();
        _ = builder.Services.RemoveAll<IHookCatalog>();
        _ = builder.Services.RemoveAll<IHookProfileSelector>();
        _ = builder.Services.RemoveAll<IHookOrderResolver>();
        _ = builder.Services.RemoveAll<IHookInstanceFactory>();
        _ = builder.Services.RemoveAll<IIdentifierGenerator<HookDispatchId>>();
        _ = builder.Services.RemoveAll<IIdentifierGenerator<HookInvocationId>>();
        _ = builder.Services.RemoveAll<IReadOnlyList<HookPointDefinitionRegistration>>();

        var exception = Should.Throw<AgentCompositionException>(builder.Build);

        exception.Diagnostics.ShouldContain(static diagnostic => diagnostic.Code == "agentkit.hook-dispatcher.missing");
        exception.Diagnostics.ShouldContain(static diagnostic => diagnostic.Code == "agentkit.hook-catalog.missing");
    }

    [Fact]
    public void Build_WhenPointDefinitionsCollide_RejectsWithHookPointCollision()
    {
        var builder = CompositionTestData.RunnableBuilder();
        _ = builder.Services.RemoveAll<IReadOnlyList<HookPointDefinitionRegistration>>();
        _ = builder.Services.AddSingleton<IReadOnlyList<HookPointDefinitionRegistration>>([
            AgentHookPointDefinitions.RunStartedRegistration,
            new HookPointDefinitionRegistration(
                AgentHookPoints.RunStarted,
                typeof(IBeforeModelRequestHook),
                typeof(BeforeModelRequestEventArgs),
                HookPointKind.Mutating,
                HookFailureMode.FailOperation),
        ]);

        var exception = Should.Throw<AgentCompositionException>(() =>
            AgentCompositionValidator.ValidateComponentRegistrations(ComponentRegistrationSnapshot.Capture(builder.Services)));

        exception.Diagnostics.ShouldContain(static diagnostic => diagnostic.Code == "agentkit.hook-point.collision");
    }
}
