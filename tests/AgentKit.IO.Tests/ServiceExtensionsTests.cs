// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO.Tests;
/// <summary>Verifies ServiceExtensions behavior and contracts.</summary>
[Collection(InputPromotionObservationGroup.Name)]
public sealed class ServiceExtensionsTests
{
    [Fact]
    public async Task AddHumanQuestionBroker_WhenIntentGeneratorIsReplaced_UsesReplacementAndRegistersOneDefault()
    {
        var services = new ServiceCollection();
        var store = new RecordingGrantStore();
        var channel = new RecordingQuestionChannel();
        var intentIds = new FixedSecurityEnforcementIntentIdGenerator(new SecurityEnforcementIntentId(Guid.Parse("90000000-0000-0000-0000-000000000002")));
        _ = services.AddSingleton<ISecurityGrantStore>(store);
        _ = services.AddSingleton<IHumanQuestionChannel>(channel);
        _ = services.AddSingleton<IIdentifierGenerator<SecurityEnforcementIntentId>>(intentIds);
        _ = services.AddHumanQuestionBroker().AddHumanQuestionBroker();
        using var provider = services.BuildServiceProvider();
        var broker = provider.GetRequiredService<IHumanQuestionBroker>();
        _ = await broker.AskAsync(Request(broker.SecurityAudience), TestContext.Current.CancellationToken);
        services.Count(descriptor => descriptor.ServiceType == typeof(IHumanQuestionBroker)).ShouldBe(1);
        store.Intents.ShouldHaveSingleItem().Id.ShouldBe(new SecurityEnforcementIntentId(Guid.Parse("90000000-0000-0000-0000-000000000002")));
    }

    private static HumanQuestionRequest Request(ComponentId audience, bool captured = false, TurnId? turnId = null)
    {
        var id = new QuestionId(Guid.Parse("10000000-0000-0000-0000-000000000001"));
        var agentId = new AgentId(Guid.Parse("20000000-0000-0000-0000-000000000002"));
        var sessionId = new SessionId(Guid.Parse("30000000-0000-0000-0000-000000000003"));
        var toolCallId = new ToolCallId(Guid.Parse("40000000-0000-0000-0000-000000000004"));
        var correlation = new InRunOperationCorrelation(new OperationId(Guid.Parse("50000000-0000-0000-0000-000000000005")), new RunId(Guid.Parse("60000000-0000-0000-0000-000000000006")), turnId);
        var identity = TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
        ImmutableArray<HumanQuestionOption> options = [new(new QuestionOptionId("one"), "One", "First option."), new(new QuestionOptionId("two"), "Two", "Second option."),];
        var deadline = DateTimeOffset.UnixEpoch.AddMinutes(5);
        var fingerprint = HumanQuestionSecurityBinding.Fingerprint(id, "Choose.", options, false, deadline);
        var scope = new SecurityAuthorizationScope(agentId, sessionId, correlation);
        var policyVersion = new SecurityPolicyVersion(1);
        var authorization = captured ? new SecurityAuthorizationContext(new SecurityProfileKey("test"), new SecurityProfileVersion(1), new SecurityPolicySnapshotReference(new SecurityPolicySnapshotId(Guid.Parse("90000000-0000-0000-0000-000000000003")), policyVersion, new ContentHash("sha256:test-policy")), new ComponentKey<ISecurityAuthority>("test"), new AgentDefinitionRevision(0), new ConfigurationVersion(1), scope, identity) : null;
        var grant = authorization is { } context ? new SecurityGrant(new GrantId(Guid.Parse("70000000-0000-0000-0000-000000000007")), new SecurityRequestId(Guid.Parse("80000000-0000-0000-0000-000000000008")), scope, identity, context, audience, SecurityOperationKind.StateMutation, SecurityEffect.Create, [HumanQuestionSecurityBinding.Resource(id)], fingerprint, policyVersion, new SecurityRevocationVersion(1), DateTimeOffset.UnixEpoch, deadline, 1) : new SecurityGrant(new GrantId(Guid.Parse("70000000-0000-0000-0000-000000000007")), new SecurityRequestId(Guid.Parse("80000000-0000-0000-0000-000000000008")), scope, identity, audience, SecurityOperationKind.StateMutation, SecurityEffect.Create, [HumanQuestionSecurityBinding.Resource(id)], fingerprint, policyVersion, new SecurityRevocationVersion(1), DateTimeOffset.UnixEpoch, deadline, 1);
        return new HumanQuestionRequest(id, agentId, sessionId, toolCallId, correlation, identity, "Choose.", options, false, deadline, grant);
    }

    [Fact]
    public void AddInputPromotionPolicy_WhenCalledTwice_RegistersOneReplaceableDefault()
    {
        var services = new ServiceCollection();
        _ = services.AddInputPromotionPolicy().AddInputPromotionPolicy();
        services.Count(descriptor => descriptor.ServiceType == typeof(IInputPromotionPolicy) && descriptor.ImplementationType == typeof(DefaultInputPromotionPolicy)).ShouldBe(1);
    }

    [Fact]
    public void AddInputPromotionPolicy_WhenServicesAreNull_ThrowsArgumentNullExceptionWithParamName()
    {
        IServiceCollection services = null!;
        var exception = Should.Throw<ArgumentNullException>(services.AddInputPromotionPolicy);
        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("services");
    }

    [Fact]
    public void AddInputCoordinator_WhenServicesAreNull_ThrowsArgumentNullExceptionWithParamName()
    {
        IServiceCollection services = null!;
        var exception = Should.Throw<ArgumentNullException>(() => services.AddInputCoordinator());
        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("services");
    }

    [Fact]
    public async Task AddInputCoordinator_WhenAQueueIsSelected_ResolvesOneReplaceableCoordinator()
    {
        var services = new ServiceCollection();
        var queue = new RecordingInputQueue();
        _ = services.AddSingleton<IInputQueue>(queue);
        _ = services.AddInputCoordinator().AddInputCoordinator();
        using var provider = services.BuildServiceProvider();

        var coordinator = provider.GetRequiredService<IInputCoordinator>();
        _ = await coordinator.AdmitAsync(InputCoordinationTestData.AdmissionRequest(), TestContext.Current.CancellationToken);

        services.Count(descriptor => descriptor.ServiceType == typeof(IInputCoordinator)).ShouldBe(1);
        queue.AppendCalls.ShouldBe(1);
    }

    [Fact]
    public void AddInputCoordinator_WhenNoQueueIsSelected_FailsAtResolutionRatherThanRegistration()
    {
        var services = new ServiceCollection();
        _ = services.AddInputCoordinator();
        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<InvalidOperationException>(provider.GetRequiredService<IInputCoordinator>);
    }

    [Fact]
    public void AddInputCoordinator_WhenCalledTwice_RegistersOneCoordinatorAndComposesConfiguration()
    {
        var services = new ServiceCollection();
        _ = services.AddInputCoordinator(static value => value.MaximumInputParts = 9)
            .AddInputCoordinator(static value => value.PreprocessingConfigurationVersion = new ConfigurationVersion(4));
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<InputCoordinatorOptions>>().Value;

        services.Count(descriptor => descriptor.ServiceType == typeof(IInputCoordinator)).ShouldBe(1);
        services.Count(descriptor => descriptor.ServiceType == typeof(IIdentifierGenerator<AdmissionId>)).ShouldBe(1);
        options.MaximumInputParts.ShouldBe(9);
        options.PreprocessingConfigurationVersion.ShouldBe(new ConfigurationVersion(4));
    }

    [Fact]
    public void AddInputCoordinator_WhenConfigureIsSupplied_AppliesItToTheBoundOptions()
    {
        var services = new ServiceCollection();
        _ = services.AddInputCoordinator(static value => value.MaximumInputParts = 3);
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<InputCoordinatorOptions>>().Value;

        options.MaximumInputParts.ShouldBe(3);
        options.PreprocessingConfigurationVersion.ShouldBe(new ConfigurationVersion(1));
    }

    [Fact]
    public void AddInputCoordinator_WhenConfigureIsOmitted_BindsTheDocumentedDefaults()
    {
        var services = new ServiceCollection();
        _ = services.AddInputCoordinator();
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<InputCoordinatorOptions>>().Value;

        options.MaximumInputParts.ShouldBe(256);
        options.PreprocessingConfigurationVersion.ShouldBe(new ConfigurationVersion(1));
    }

    [Fact]
    public void AddInputCoordinator_WhenConfiguredPartBoundIsNotPositive_FailsOptionsValidation()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<IInputQueue>(new RecordingInputQueue());
        _ = services.AddInputCoordinator(static value => value.MaximumInputParts = 0);
        using var provider = services.BuildServiceProvider();

        var optionsFailure = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<InputCoordinatorOptions>>().Value);
        var coordinatorFailure = Should.Throw<OptionsValidationException>(provider.GetRequiredService<IInputCoordinator>);

        optionsFailure.Failures.ShouldContain("MaximumInputParts must be at least 1.");
        coordinatorFailure.OptionsType.ShouldBe(typeof(InputCoordinatorOptions));
    }

    [Fact]
    public void AddInputCoordinator_WhenConfiguredPreprocessingRevisionIsDefault_FailsOptionsValidation()
    {
        var services = new ServiceCollection();
        _ = services.AddInputCoordinator(static value => value.PreprocessingConfigurationVersion = default);
        using var provider = services.BuildServiceProvider();

        var failure = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<InputCoordinatorOptions>>().Value);

        failure.Failures.ShouldContain("PreprocessingConfigurationVersion must be set.");
    }

    [Fact]
    public void AddInputCoordinator_WhenOptionsInstanceOverloadIsUsed_AppliesTheInstanceValues()
    {
        var services = new ServiceCollection();
        _ = services.AddInputCoordinator(new InputCoordinatorOptions(new ConfigurationVersion(6), 5));
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<InputCoordinatorOptions>>().Value;

        options.MaximumInputParts.ShouldBe(5);
        options.PreprocessingConfigurationVersion.ShouldBe(new ConfigurationVersion(6));
    }

    [Fact]
    public void AddInputCoordinator_WhenOptionsInstanceIsNull_ThrowsArgumentNullExceptionWithParamName()
    {
        var services = new ServiceCollection();

        var exception = Should.Throw<ArgumentNullException>(() => services.AddInputCoordinator((InputCoordinatorOptions) null!));

        exception.ParamName.ShouldBe("options");
        services.ShouldBeEmpty();
    }

    [Fact]
    public void AddInputCoordinator_WhenOptionsInstanceOverloadReceivesNullServices_ThrowsArgumentNullExceptionWithParamName()
    {
        IServiceCollection services = null!;

        var exception = Should.Throw<ArgumentNullException>(() => services.AddInputCoordinator(new InputCoordinatorOptions()));

        exception.ParamName.ShouldBe("services");
    }

    [Fact]
    public async Task AddInputCoordinator_WhenHostConfiguresOptionsBeforeRegistration_HonorsTheHostConfiguration()
    {
        var services = new ServiceCollection();
        var queue = new RecordingInputQueue();
        _ = services.AddSingleton<IInputQueue>(queue);
        _ = services.Configure<InputCoordinatorOptions>(static value => value.PreprocessingConfigurationVersion = new ConfigurationVersion(11));
        _ = services.AddInputCoordinator();
        using var provider = services.BuildServiceProvider();

        var coordinator = provider.GetRequiredService<IInputCoordinator>();
        _ = await coordinator.AdmitAsync(InputCoordinationTestData.AdmissionRequest(), TestContext.Current.CancellationToken);

        queue.AppendedPreprocessing!.ConfigurationVersion.ShouldBe(new ConfigurationVersion(11));
    }

    [Fact]
    public async Task AddInputCoordinator_WhenHostConfiguresOptionsAfterRegistration_AppliesTheLaterConfiguration()
    {
        var services = new ServiceCollection();
        var queue = new RecordingInputQueue();
        _ = services.AddSingleton<IInputQueue>(queue);
        _ = services.AddInputCoordinator(static value => value.PreprocessingConfigurationVersion = new ConfigurationVersion(2));
        _ = services.Configure<InputCoordinatorOptions>(static value => value.PreprocessingConfigurationVersion = new ConfigurationVersion(11));
        using var provider = services.BuildServiceProvider();

        var coordinator = provider.GetRequiredService<IInputCoordinator>();
        _ = await coordinator.AdmitAsync(InputCoordinationTestData.AdmissionRequest(), TestContext.Current.CancellationToken);

        queue.AppendedPreprocessing!.ConfigurationVersion.ShouldBe(new ConfigurationVersion(11));
    }

    private static readonly ComponentKey<IInputCoordinator> InputKey = new("test-input");
    private static readonly ComponentKey<IOutputPublisher> OutputKey = new("test-output");

    [Fact]
    public async Task AddAgentIO_WhenCalled_ResolvesTheKeyedDefaultPair()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<IInputQueue>(new RecordingInputQueue());
        _ = services.AddSingleton<ILogger<DefaultOutputPublisher>>(Microsoft.Extensions.Logging.Abstractions.NullLogger<DefaultOutputPublisher>.Instance);
        _ = services.AddScoped(_ => SampleIdentity());
        _ = services.AddAgentIO(InputKey, OutputKey);
        using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        _ = scope.ServiceProvider.GetRequiredKeyedService<IInputCoordinator>(InputKey.Value).ShouldBeOfType<DefaultInputCoordinator>();
        _ = scope.ServiceProvider.GetRequiredKeyedService<IOutputPublisher>(OutputKey.Value).ShouldBeOfType<DefaultOutputPublisher>();
    }

    [Fact]
    public void AddAgentIO_WhenCalledTwice_KeepsFirstRegistration()
    {
        var services = new ServiceCollection();

        _ = services.AddAgentIO(InputKey, OutputKey);
        _ = services.AddAgentIO(InputKey, OutputKey);

        services.Count(descriptor => descriptor.IsKeyedService && descriptor.ServiceType == typeof(IInputCoordinator)
            && InputKey.Value.Equals(descriptor.ServiceKey)).ShouldBe(1);
        services.Count(descriptor => descriptor.IsKeyedService && descriptor.ServiceType == typeof(IOutputPublisher)
            && OutputKey.Value.Equals(descriptor.ServiceKey)).ShouldBe(1);
    }

    [Fact]
    public void AddAgentIO_WhenAConflictingInputCoordinatorIsAlreadyRegisteredUnderTheSameKey_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        _ = services.AddInputCoordinator<FakeInputCoordinator>(InputKey);

        var exception = Should.Throw<InvalidOperationException>(() => services.AddAgentIO(InputKey, OutputKey));

        exception.Message.ShouldContain(InputKey.Value);
    }

    [Fact]
    public void AddAgentIO_WhenAConflictingOutputPublisherIsAlreadyRegisteredUnderTheSameKey_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        _ = services.AddOutputPublisher<FakeOutputPublisher>(OutputKey);

        var exception = Should.Throw<InvalidOperationException>(() => services.AddAgentIO(InputKey, OutputKey));

        exception.Message.ShouldContain(OutputKey.Value);
    }

    [Fact]
    public void AddInputCoordinator_WhenKeyedAndAConflictingImplementationIsAlreadyRegisteredUnderTheSameKey_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        _ = services.AddInputCoordinator<FakeInputCoordinator>(InputKey);

        var exception = Should.Throw<InvalidOperationException>(() => services.AddInputCoordinator<DefaultInputCoordinator>(InputKey));

        exception.Message.ShouldContain(InputKey.Value);
    }

    [Fact]
    public void ReplaceInputCoordinator_WhenCalled_ReplacesTheExistingKeyedRegistration()
    {
        var services = new ServiceCollection();
        _ = services.AddInputCoordinator<DefaultInputCoordinator>(InputKey);

        _ = services.ReplaceInputCoordinator<FakeInputCoordinator>(InputKey);

        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredKeyedService<IInputCoordinator>(InputKey.Value).ShouldBeOfType<FakeInputCoordinator>();
    }

    [Fact]
    public void AddOutputPublisher_WhenKeyedAndAConflictingImplementationIsAlreadyRegisteredUnderTheSameKey_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        _ = services.AddOutputPublisher<FakeOutputPublisher>(OutputKey);

        var exception = Should.Throw<InvalidOperationException>(() => services.AddOutputPublisher<DefaultOutputPublisher>(OutputKey));

        exception.Message.ShouldContain(OutputKey.Value);
    }

    [Fact]
    public void ReplaceOutputPublisher_WhenCalled_ReplacesTheExistingKeyedRegistration()
    {
        var services = new ServiceCollection();
        _ = services.AddOutputPublisher<FakeOutputPublisher>(OutputKey);

        _ = services.ReplaceOutputPublisher<FakeOutputPublisher>(OutputKey);

        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredKeyedService<IOutputPublisher>(OutputKey.Value).ShouldBeOfType<FakeOutputPublisher>();
    }

    [Fact]
    public void AddRunEventSink_WhenCalledTwiceWithAnEquivalentRegistration_IsIdempotent()
    {
        var services = new ServiceCollection();
        var registration = new RunEventSinkRegistration("sink", RunEventDelivery.BestEffort, 0);

        _ = services.AddRunEventSink<FakeRunEventSink>(registration);
        _ = services.AddRunEventSink<FakeRunEventSink>(registration);

        services.Count(descriptor => descriptor.ServiceType == typeof(IRunEventSink)).ShouldBe(1);
    }

    [Fact]
    public void AddRunEventSink_WhenADifferentRegistrationReusesTheSameName_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        _ = services.AddRunEventSink<FakeRunEventSink>(new RunEventSinkRegistration("sink", RunEventDelivery.BestEffort, 0));

        var exception = Should.Throw<InvalidOperationException>(() => services.AddRunEventSink<FakeRunEventSink>(
            new RunEventSinkRegistration("sink", RunEventDelivery.Required, 0)));

        exception.Message.ShouldContain("sink");
    }

    [Fact]
    public void AddRunEventSink_WhenADifferentImplementationTypeReusesTheSameName_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        var registration = new RunEventSinkRegistration("sink", RunEventDelivery.BestEffort, 0);
        _ = services.AddRunEventSink<FakeRunEventSink>(registration);

        var exception = Should.Throw<InvalidOperationException>(() => services.AddRunEventSink<OtherFakeRunEventSink>(registration));

        exception.Message.ShouldContain("sink");
    }

    [Fact]
    public void AddRunEventSink_WhenServicesAreNull_ThrowsArgumentNullExceptionWithParamName()
    {
        IServiceCollection services = null!;
        var exception = Should.Throw<ArgumentNullException>(
            () => services.AddRunEventSink<FakeRunEventSink>(new RunEventSinkRegistration("sink", RunEventDelivery.BestEffort, 0)));
        exception.ParamName.ShouldBe("services");
    }

    [Fact]
    public void AddRunEventSink_WhenRegistrationIsNull_ThrowsArgumentNullExceptionWithParamName()
    {
        var services = new ServiceCollection();
        var exception = Should.Throw<ArgumentNullException>(() => services.AddRunEventSink<FakeRunEventSink>(null!));
        exception.ParamName.ShouldBe("registration");
    }

    private static RunScopeIdentity SampleIdentity() => new(
        new AgentId(Guid.Parse("10000000-0000-0000-0000-000000000001")),
        new SessionId(Guid.Parse("20000000-0000-0000-0000-000000000001")),
        null,
        new RunId(Guid.Parse("30000000-0000-0000-0000-000000000001")));

    private sealed class OtherFakeRunEventSink: IRunEventSink
    {
        public ValueTask PublishAsync(RunEvent runEvent, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }
}
