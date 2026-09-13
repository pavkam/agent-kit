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
    public async Task AddInputCoordinator_WhenCollaboratorsAreReplaced_UsesTheReplacements()
    {
        var services = new ServiceCollection();
        var queue = new RecordingInputQueue();
        var options = new InputCoordinatorOptions(new ConfigurationVersion(11));
        _ = services.AddSingleton<IInputQueue>(queue);
        _ = services.AddSingleton(options);
        _ = services.AddInputCoordinator(new InputCoordinatorOptions(new ConfigurationVersion(2)));
        using var provider = services.BuildServiceProvider();

        var coordinator = provider.GetRequiredService<IInputCoordinator>();
        _ = await coordinator.AdmitAsync(InputCoordinationTestData.AdmissionRequest(), TestContext.Current.CancellationToken);

        queue.AppendedPreprocessing!.ConfigurationVersion.ShouldBe(new ConfigurationVersion(11));
    }
}
