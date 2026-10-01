// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

using AgentKit.Internal;


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
        CompositionTestData.AddFacadeRegistrationRequirements(services, includeGrantStore: false);
        _ = services.RemoveAll<ISecurityProfileSelector>();
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
        CompositionTestData.AddFacadeRegistrationRequirements(services, includeGrantStore: false);
        _ = services.RemoveAll<ISecurityProfileSelector>();
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
        public ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(
            SecurityGrant grant,
            SecurityEnforcementRequest enforcement,
            SecurityEnforcementIntent intent,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(grant);
            ArgumentNullException.ThrowIfNull(enforcement);
            ArgumentNullException.ThrowIfNull(intent);
            return ValueTask.FromResult(new GrantConsumptionResult(
                GrantConsumptionStatus.Unknown, 0, "This test grant store does not consume grants.", null));
        }

        public ValueTask RegisterAsync(SecurityGrant grant, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask<GrantRevocationResult> RevokeAsync(GrantId grantId, RevocationReason reason, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(reason);
            return ValueTask.FromResult<GrantRevocationResult>(new GrantRevocationNotFound(grantId));
        }
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
    [InlineData(typeof(IModelCatalog), "agentkit.model-catalog")]
    [InlineData(typeof(IProviderProfileRuntimeSelector), "agentkit.provider-profile-selector")]
    [InlineData(typeof(ISecurityAuthoritySelector), "agentkit.security-authority-selector")]
    [InlineData(typeof(ISecurityPolicyCatalog), "agentkit.security-policy-catalog")]
    [InlineData(typeof(IApprovalBroker), "agentkit.approval-broker")]
    [InlineData(typeof(IAgentRunScopeFactory), "agentkit.run-scope-factory")]
    [InlineData(typeof(ISessionDirectory), "agentkit.session-directory")]
    [InlineData(typeof(ISessionStoreCatalog), "agentkit.session-store-catalog")]
    [InlineData(typeof(ISessionStoreSelector), "agentkit.session-store-selector")]
    [InlineData(typeof(IBudgetAuthority), "agentkit.budget-authority")]
    [InlineData(typeof(IBudgetProfileCatalog), "agentkit.budget-profile-catalog")]
    [InlineData(typeof(IRandomizerFactory), "agentkit.randomizer-factory")]
    [InlineData(typeof(IContentHasher), "agentkit.content-hasher")]
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

    [Theory]
    [InlineData(typeof(IAgentDefinitionCatalog), "agentkit.catalog")]
    [InlineData(typeof(IAgentRunProfilePublicationReader), "agentkit.run-profile-reader")]
    [InlineData(typeof(ISecurityProfileSelector), "agentkit.security-profile-selector")]
    [InlineData(typeof(ISecurityGrantStore), "agentkit.security-grant-store")]
    [InlineData(typeof(TimeProvider), "agentkit.time")]
    [InlineData(typeof(IIdentifierGenerator<RunId>), "agentkit.runid")]
    [InlineData(typeof(IIdentifierGenerator<OperationId>), "agentkit.operationid")]
    [InlineData(typeof(IModelCatalog), "agentkit.model-catalog")]
    [InlineData(typeof(IProviderProfileRuntimeSelector), "agentkit.provider-profile-selector")]
    [InlineData(typeof(ISecurityAuthoritySelector), "agentkit.security-authority-selector")]
    [InlineData(typeof(ISecurityPolicyCatalog), "agentkit.security-policy-catalog")]
    [InlineData(typeof(IApprovalBroker), "agentkit.approval-broker")]
    [InlineData(typeof(IAgentRunScopeFactory), "agentkit.run-scope-factory")]
    [InlineData(typeof(ISessionDirectory), "agentkit.session-directory")]
    [InlineData(typeof(ISessionStoreCatalog), "agentkit.session-store-catalog")]
    [InlineData(typeof(ISessionStoreSelector), "agentkit.session-store-selector")]
    [InlineData(typeof(IBudgetAuthority), "agentkit.budget-authority")]
    [InlineData(typeof(IBudgetProfileCatalog), "agentkit.budget-profile-catalog")]
    [InlineData(typeof(IRandomizerFactory), "agentkit.randomizer-factory")]
    [InlineData(typeof(IContentHasher), "agentkit.content-hasher")]
    [InlineData(typeof(AgentEngine), "agentkit.engine")]
    public void ValidateComponentRegistrations_WhenSingularServiceIsRegisteredTwice_ReportsAmbiguousWithoutFactories(Type serviceType, string code)
    {
        var builder = CompositionTestData.RunnableBuilder();
        var factoryCalls = 0;
        builder.Services.Add(ServiceDescriptor.Singleton(serviceType, _ =>
        {
            factoryCalls++;
            throw new InvalidOperationException("Validation must not invoke a duplicate registration's factory.");
        }));

        var exception = Should.Throw<AgentCompositionException>(() => AgentCompositionValidator.ValidateComponentRegistrations(ComponentRegistrationSnapshot.Capture(builder.Services)));

        exception.Diagnostics.ShouldContain(diagnostic => diagnostic.Code == $"{code}.ambiguous");
        factoryCalls.ShouldBe(0);
    }

    [Fact]
    public void ValidateComponentRegistrations_WhenEngineConstructionRequiresTheFacadeButItsDescriptorWasRemoved_ReportsEngineMissing()
    {
        var builder = CompositionTestData.RunnableBuilder();
        _ = builder.Services.RemoveAll<AgentEngine>();

        var exception = Should.Throw<AgentCompositionException>(() => AgentCompositionValidator.ValidateComponentRegistrations(
            ComponentRegistrationSnapshot.Capture(builder.Services), requiresFacade: true));

        exception.Diagnostics.ShouldContain(static diagnostic => diagnostic.Code == "agentkit.engine.missing");
    }

    [Fact]
    public void ValidateComponentRegistrations_WhenLoopIsOnlyRegisteredUnderAnUnselectedKey_AcceptsTheNarrowFacadeCheck()
    {
        // ValidateComponentRegistrations alone (unlike the full AgentCompositionValidator.Validate used at engine
        // build time) does not know which key an agent definition selects, so it only proves that some keyed
        // IAgentLoop registration exists. Whether it is the exact key a definition selects is proven separately
        // by DefinitionCompositionValidator, exercised end-to-end by
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
        CompositionTestData.AddFacadeRegistrationRequirements(services);
        _ = services.RemoveAll<ISecurityProfileSelector>();
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

    [Fact]
    public void Build_WhenDefinitionSelectsUnknownHookProfile_RejectsWithHookProfileUnavailable()
    {
        var definition = CompositionTestData.Definition() with { HookProfile = new HookProfileKey("unknown-profile") };
        var builder = CompositionTestData.RunnableBuilder(definition: definition);

        var exception = Should.Throw<AgentCompositionException>(builder.Build);

        exception.Diagnostics.ShouldContain(static diagnostic => diagnostic.Code == "agentkit.hook-profile.unavailable");
    }

    [Fact]
    public void Build_WhenNoCandidateSatisfiesModelRequirements_RejectsWithIncompatibleDiagnostic()
    {
        var descriptor = new ModelDescriptor(
            new ModelAlias("chat"),
            new ProviderId("test"),
            new ApiFamilyId("test"),
            new ModelId("m"),
            deploymentId: null,
            new ModelCapabilities(
                supportsSystemInstructions: true,
                supportsStreaming: true,
                supportsToolCalls: false,
                supportsParallelToolCalls: false,
                supportsStructuredOutput: false,
                supportsReasoning: false,
                supportsVisionInput: false,
                ExtensionData.Empty),
            new ModelLimits(maxContextTokens: null, maxOutputTokens: null),
            pricing: null,
            ExtensionData.Empty);
        var definition = CompositionTestData.Definition() with
        {
            Models = new ModelSelectionPolicy([new ModelAlias("chat")], requirements: new ModelRequirements { RequiresToolCalls = true }),
        };
        var builder = CompositionTestData.RunnableBuilder(definition: definition);
        _ = builder.Services.RemoveAll<IModelCatalog>();
        _ = builder.Services.AddSingleton<IModelCatalog>(
            new StaticModelCatalog(new ModelCatalogSnapshot(new ModelCatalogVersion(1), [descriptor])));
        _ = builder.Services.RemoveAll<IModelCapabilityValidator>();
        _ = builder.Services.AddSingleton<IModelCapabilityValidator, ToolRequirementCapabilityValidator>();

        var exception = Should.Throw<AgentCompositionException>(builder.Build);

        exception.Diagnostics.ShouldContain(static diagnostic => diagnostic.Code == "agentkit.definition.model-incompatible");
    }

    private sealed class ToolRequirementCapabilityValidator: IModelCapabilityValidator
    {
        public ValueTask<CapabilityValidationResult> ValidateAsync(
            ModelDescriptor model,
            ModelRequirements requirements,
            CapabilityDowngradePolicy downgradePolicy,
            CancellationToken cancellationToken = default)
        {
            return requirements.RequiresToolCalls && !model.Capabilities.SupportsToolCalls
                ? ValueTask.FromResult<CapabilityValidationResult>(
                    new CapabilitiesUnsupported([
                        new UnsupportedCapability(
                            ModelCapabilityKind.ToolCalls,
                            "The request requires model-requested tool calls."),
                    ]))
                : ValueTask.FromResult<CapabilityValidationResult>(new CapabilitiesSupported());
        }
    }
}
