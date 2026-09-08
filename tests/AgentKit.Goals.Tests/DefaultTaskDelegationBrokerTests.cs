// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Tests;

public sealed class DefaultTaskDelegationBrokerTests
{
    [Fact]
    public void Constructor_WhenIntentIdsIsNull_ThrowsWithExactParameterName()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new DefaultTaskDelegationBroker(
            new RecordingGrantStore(), new RecordingDelegationChannel(), null!));

        exception.ParamName.ShouldBe("intentIds");
    }

    [Fact]
    public async Task DelegateAsync_WhenGrantMatches_ConsumesFreshExactEvidenceBeforeGrantFreeDispatch()
    {
        var store = new RecordingGrantStore();
        var channel = new RecordingDelegationChannel();
        var intentId = new SecurityEnforcementIntentId(Guid.Parse("e0000000-0000-0000-0000-00000000000e"));
        var broker = new DefaultTaskDelegationBroker(store, channel, new FixedSecurityEnforcementIntentIdGenerator(intentId));
        var request = Request(broker.SecurityAudience);

        var result = await broker.DelegateAsync(request, TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<TaskDelegationChildResult>();
        var enforcement = store.Enforcements.ShouldHaveSingleItem();
        enforcement.Kind.ShouldBe(SecurityOperationKind.Delegation);
        enforcement.Effect.ShouldBe(SecurityEffect.Create);
        enforcement.Resources.ShouldBe([TaskDelegationSecurityBinding.Resource(request.Prompt.Id)]);
        enforcement.InputFingerprint.ShouldBe(request.Grant.InputFingerprint);
        store.Intents.ShouldHaveSingleItem().Id.ShouldBe(intentId);
        channel.Prompts.ShouldHaveSingleItem().ShouldBe(request.Prompt);
    }

    [Fact]
    public async Task DelegateAsync_WhenGrantCannotBeConsumed_PerformsNoDispatchAndInventsNoChildIdentity()
    {
        var store = new RecordingGrantStore { Status = GrantConsumptionStatus.Exhausted };
        var channel = new RecordingDelegationChannel();
        var broker = new DefaultTaskDelegationBroker(store, channel);

        var result = await broker.DelegateAsync(Request(broker.SecurityAudience), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<TaskDelegationRejected>().SafeMessage.ShouldBe("Grant Exhausted.");
        channel.Prompts.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(GrantConsumptionStatus.Reconciled, true, true)]
    [InlineData(GrantConsumptionStatus.Consumed, false, true)]
    [InlineData(GrantConsumptionStatus.Consumed, true, false)]
    public async Task DelegateAsync_WhenReceiptDoesNotAuthorizeFreshIntent_PerformsNoDispatch(
        GrantConsumptionStatus status,
        bool includeReceipt,
        bool exactReceipt)
    {
        var store = new RecordingGrantStore
        {
            Status = status,
            IncludeReceipt = includeReceipt,
            ReturnExactReceipt = exactReceipt,
        };
        var channel = new RecordingDelegationChannel();
        var broker = new DefaultTaskDelegationBroker(store, channel);

        var result = await broker.DelegateAsync(Request(broker.SecurityAudience), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<TaskDelegationRejected>();
        _ = store.Intents.ShouldHaveSingleItem();
        channel.Prompts.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("scope")]
    [InlineData("identity")]
    public async Task DelegateAsync_WhenCapturedAuthorizationDoesNotMatch_DeniesBeforeIntentConsumptionOrDispatch(
        string mismatch)
    {
        var store = new RecordingGrantStore();
        var channel = new RecordingDelegationChannel();
        var intentIds = new FixedSecurityEnforcementIntentIdGenerator(
            new SecurityEnforcementIntentId(Guid.Parse("e0000000-0000-0000-0000-00000000000b")));
        var broker = new DefaultTaskDelegationBroker(store, channel, intentIds);
        var request = Request(broker.SecurityAudience, captured: true);
        var mismatched = mismatch == "scope"
            ? request with
            {
                Prompt = request.Prompt with
                {
                    ParentAgentId = new AgentId(Guid.Parse("e0000000-0000-0000-0000-00000000000c")),
                },
            }
            : request with
            {
                Prompt = request.Prompt with
                {
                    Identity = TestSupport.TestExecutionIdentity.Create(
                        new TenantId("other-tenant"), new PrincipalId("other-principal"), ExecutionSubjectKind.Human),
                },
            };

        var result = await broker.DelegateAsync(mismatched, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<TaskDelegationRejected>().SafeMessage.ShouldBe(
            "The captured authorization does not match the task delegation.");
        intentIds.Calls.ShouldBe(0);
        store.Enforcements.ShouldBeEmpty();
        channel.Prompts.ShouldBeEmpty();
    }

    [Fact]
    public async Task DelegateAsync_WhenEnvelopeChangesAfterAuthorization_FailsClosedBeforeDispatch()
    {
        var store = new RecordingGrantStore();
        var channel = new RecordingDelegationChannel();
        var broker = new DefaultTaskDelegationBroker(store, channel);
        var request = Request(broker.SecurityAudience);
        request = request with { Prompt = request.Prompt with { Objective = "Different objective." } };

        var result = await broker.DelegateAsync(request, TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<TaskDelegationRejected>();
        channel.Prompts.ShouldBeEmpty();
    }

    [Fact]
    public async Task DelegateAsync_WhenCallerCancelsDuringNonCooperativeConsumption_PropagatesBeforeDispatch()
    {
        using var cancellation = new CancellationTokenSource();
        var store = new RecordingGrantStore { OnConsume = cancellation.Cancel };
        var channel = new RecordingDelegationChannel();
        var broker = new DefaultTaskDelegationBroker(store, channel);

        var action = async () => await broker.DelegateAsync(Request(broker.SecurityAudience), cancellation.Token);

        _ = await action.ShouldThrowAsync<OperationCanceledException>();
        _ = store.Intents.ShouldHaveSingleItem();
        channel.Prompts.ShouldBeEmpty();
    }

    [Fact]
    public async Task DelegateAsync_WhenAlreadyCancelled_PerformsNoConsumptionOrDispatch()
    {
        var store = new RecordingGrantStore();
        var channel = new RecordingDelegationChannel();
        var broker = new DefaultTaskDelegationBroker(store, channel);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var action = () => broker.DelegateAsync(Request(broker.SecurityAudience), cancellation.Token).AsTask();

        _ = await action.ShouldThrowAsync<OperationCanceledException>();
        store.Enforcements.ShouldBeEmpty();
        channel.Prompts.ShouldBeEmpty();
    }

    [Fact]
    public async Task DelegateAsync_WhenCapturedGrantIsRegistered_ConsumesItsExactAuthorizationBeforeDispatch()
    {
        var store = new InMemorySecurityGrantStore(new FixedTimeProvider());
        var channel = new RecordingDelegationChannel();
        var broker = new DefaultTaskDelegationBroker(store, channel);
        var request = Request(broker.SecurityAudience, captured: true);
        await store.RegisterAsync(request.Grant, TestContext.Current.CancellationToken);

        var result = await broker.DelegateAsync(request, TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<TaskDelegationChildResult>();
        channel.Prompts.ShouldHaveSingleItem().ShouldBe(request.Prompt);
    }

    [Fact]
    public async Task AddAgentDelegation_WhenIntentGeneratorIsReplaced_UsesReplacementAndRegistersOneDefaultBroker()
    {
        var services = new ServiceCollection();
        var store = new RecordingGrantStore();
        var channel = new RecordingDelegationChannel();
        var intentId = new SecurityEnforcementIntentId(Guid.Parse("e0000000-0000-0000-0000-00000000000e"));
        _ = services.AddSingleton<ISecurityGrantStore>(store);
        _ = services.AddSingleton<ITaskDelegationChannel>(channel);
        _ = services.AddSingleton<IIdentifierGenerator<SecurityEnforcementIntentId>>(
            new FixedSecurityEnforcementIntentIdGenerator(intentId));
        _ = services.AddAgentDelegation().AddAgentDelegation();
        using var provider = services.BuildServiceProvider();
        var broker = provider.GetRequiredService<ITaskDelegationBroker>();

        _ = await broker.DelegateAsync(Request(broker.SecurityAudience), TestContext.Current.CancellationToken);

        services.Count(descriptor => descriptor.ServiceType == typeof(ITaskDelegationBroker)).ShouldBe(1);
        store.Intents.ShouldHaveSingleItem().Id.ShouldBe(intentId);
    }

    private static TaskDelegationRequest Request(ComponentId audience, bool captured = false)
    {
        var prompt = new TaskDelegationPrompt(
            new DelegationId(Guid.Parse("10000000-0000-0000-0000-000000000001")),
            new AgentId(Guid.Parse("20000000-0000-0000-0000-000000000002")),
            new SessionId(Guid.Parse("30000000-0000-0000-0000-000000000003")),
            new RunId(Guid.Parse("40000000-0000-0000-0000-000000000004")),
            new InRunOperationCorrelation(
                new OperationId(Guid.Parse("50000000-0000-0000-0000-000000000005")),
                new RunId(Guid.Parse("40000000-0000-0000-0000-000000000004")), null),
            new ToolCallId(Guid.Parse("60000000-0000-0000-0000-000000000006")),
            TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human),
            new AgentId(Guid.Parse("70000000-0000-0000-0000-000000000007")),
            "Implement the parser.", ["Tests pass."], [new ToolId("read")], new TaskDelegationBudget(10, 20),
            DateTimeOffset.UnixEpoch.AddMinutes(5));
        var scope = new SecurityAuthorizationScope(prompt.ParentAgentId, prompt.ParentSessionId, prompt.Correlation);
        var policyVersion = new SecurityPolicyVersion(1);
        var authorization = captured
            ? new SecurityAuthorizationContext(
                new SecurityProfileKey("test"), new SecurityProfileVersion(1),
                new SecurityPolicySnapshotReference(
                    new SecurityPolicySnapshotId(Guid.Parse("d0000000-0000-0000-0000-00000000000d")),
                    policyVersion, new ContentHash("sha256:test-policy")),
                new ComponentKey<ISecurityAuthority>("test"), new AgentDefinitionRevision(0),
                new ConfigurationVersion(1), scope, prompt.Identity)
            : null;
        var grant = authorization is { } context
            ? new SecurityGrant(
                new GrantId(Guid.Parse("80000000-0000-0000-0000-000000000008")),
                new SecurityRequestId(Guid.Parse("90000000-0000-0000-0000-000000000009")),
                scope, prompt.Identity, context, audience, SecurityOperationKind.Delegation, SecurityEffect.Create,
                [TaskDelegationSecurityBinding.Resource(prompt.Id)], TaskDelegationSecurityBinding.Fingerprint(prompt),
                policyVersion, new SecurityRevocationVersion(1), DateTimeOffset.UnixEpoch, prompt.Deadline, 1)
            : new SecurityGrant(
                new GrantId(Guid.Parse("80000000-0000-0000-0000-000000000008")),
                new SecurityRequestId(Guid.Parse("90000000-0000-0000-0000-000000000009")),
                scope, prompt.Identity, audience, SecurityOperationKind.Delegation, SecurityEffect.Create,
                [TaskDelegationSecurityBinding.Resource(prompt.Id)], TaskDelegationSecurityBinding.Fingerprint(prompt),
                policyVersion, new SecurityRevocationVersion(1), DateTimeOffset.UnixEpoch, prompt.Deadline, 1);
        return new TaskDelegationRequest(prompt, grant);
    }
}
