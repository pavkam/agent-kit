// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO.Tests;

public sealed class DefaultHumanQuestionBrokerTests
{
    [Fact]
    public void Constructor_WhenIntentIdsIsNull_ThrowsWithExactParameterName()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new DefaultHumanQuestionBroker(
            new RecordingGrantStore(), new RecordingQuestionChannel(), null!));

        exception.ParamName.ShouldBe("intentIds");
    }

    [Fact]
    public async Task AskAsync_WhenGrantMatches_ConsumesFreshExactEvidenceBeforePublishingGrantFreePrompt()
    {
        var store = new RecordingGrantStore();
        var channel = new RecordingQuestionChannel();
        var intentIds = new FixedSecurityEnforcementIntentIdGenerator(
            new SecurityEnforcementIntentId(Guid.Parse("90000000-0000-0000-0000-000000000001")));
        var broker = new DefaultHumanQuestionBroker(store, channel, intentIds);
        var request = Request(broker.SecurityAudience);

        var result = await broker.AskAsync(request, TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<HumanQuestionAnswered>();
        var enforcement = store.Enforcements.ShouldHaveSingleItem();
        enforcement.Audience.ShouldBe(broker.SecurityAudience);
        enforcement.Resources.ShouldBe([HumanQuestionSecurityBinding.Resource(request.Id)]);
        enforcement.InputFingerprint.ShouldBe(request.Grant.InputFingerprint);
        store.Intents.ShouldHaveSingleItem().Id.ShouldBe(intentIds.Create());
        var prompt = channel.Prompts.ShouldHaveSingleItem();
        prompt.Id.ShouldBe(request.Id);
        prompt.Prompt.ShouldBe(request.Prompt);
    }

    [Fact]
    public async Task AskAsync_WhenGrantCannotBeConsumed_PerformsNoPublication()
    {
        var store = new RecordingGrantStore { Status = GrantConsumptionStatus.Exhausted };
        var channel = new RecordingQuestionChannel();
        var broker = new DefaultHumanQuestionBroker(store, channel);

        var result = await broker.AskAsync(
            Request(broker.SecurityAudience), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<HumanQuestionUnavailable>().SafeMessage.ShouldBe("Grant Exhausted.");
        channel.Prompts.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(GrantConsumptionStatus.Reconciled, true, true)]
    [InlineData(GrantConsumptionStatus.Consumed, false, true)]
    [InlineData(GrantConsumptionStatus.Consumed, true, false)]
    public async Task AskAsync_WhenReceiptDoesNotAuthorizeFreshIntent_PerformsNoPublication(
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
        var channel = new RecordingQuestionChannel();
        var broker = new DefaultHumanQuestionBroker(store, channel);

        var result = await broker.AskAsync(Request(broker.SecurityAudience), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<HumanQuestionUnavailable>();
        _ = store.Intents.ShouldHaveSingleItem();
        channel.Prompts.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("scope")]
    [InlineData("identity")]
    public async Task AskAsync_WhenCapturedAuthorizationDoesNotMatch_DeniesBeforeIntentConsumptionOrPublication(
        string mismatch)
    {
        var store = new RecordingGrantStore();
        var channel = new RecordingQuestionChannel();
        var intentIds = new FixedSecurityEnforcementIntentIdGenerator(
            new SecurityEnforcementIntentId(Guid.Parse("90000000-0000-0000-0000-000000000004")));
        var broker = new DefaultHumanQuestionBroker(store, channel, intentIds);
        var request = Request(broker.SecurityAudience, captured: true);
        var mismatched = mismatch == "scope"
            ? request with { AgentId = new AgentId(Guid.Parse("90000000-0000-0000-0000-000000000005")) }
            : request with
            {
                Identity = TestSupport.TestExecutionIdentity.Create(
                    new TenantId("other-tenant"), new PrincipalId("other-principal"), ExecutionSubjectKind.Human),
            };

        var result = await broker.AskAsync(mismatched, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<HumanQuestionUnavailable>().SafeMessage.ShouldBe(
            "The captured authorization does not match the question publication.");
        intentIds.Calls.ShouldBe(0);
        store.Enforcements.ShouldBeEmpty();
        channel.Prompts.ShouldBeEmpty();
    }

    [Fact]
    public async Task AskAsync_WhenRequestChangesAfterAuthorization_FailsClosedBeforePublication()
    {
        var store = new RecordingGrantStore();
        var channel = new RecordingQuestionChannel();
        var broker = new DefaultHumanQuestionBroker(store, channel);
        var request = Request(broker.SecurityAudience) with { Prompt = "A different question." };

        var result = await broker.AskAsync(request, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<HumanQuestionUnavailable>().SafeMessage.ShouldBe("Grant Mismatch.");
        channel.Prompts.ShouldBeEmpty();
    }

    [Fact]
    public async Task AskAsync_WhenCallerCancelsDuringNonCooperativeConsumption_PropagatesBeforePublication()
    {
        using var cancellation = new CancellationTokenSource();
        var store = new RecordingGrantStore { OnConsume = cancellation.Cancel };
        var channel = new RecordingQuestionChannel();
        var broker = new DefaultHumanQuestionBroker(store, channel);

        var action = async () => await broker.AskAsync(Request(broker.SecurityAudience), cancellation.Token);

        _ = await action.ShouldThrowAsync<OperationCanceledException>();
        _ = store.Intents.ShouldHaveSingleItem();
        channel.Prompts.ShouldBeEmpty();
    }

    [Fact]
    public async Task AskAsync_WhenAlreadyCancelled_PerformsNoGrantConsumptionOrPublication()
    {
        var store = new RecordingGrantStore();
        var channel = new RecordingQuestionChannel();
        var broker = new DefaultHumanQuestionBroker(store, channel);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var action = () => broker.AskAsync(Request(broker.SecurityAudience), cancellation.Token).AsTask();

        _ = await action.ShouldThrowAsync<OperationCanceledException>();
        store.Enforcements.ShouldBeEmpty();
        channel.Prompts.ShouldBeEmpty();
    }

    [Fact]
    public async Task AskAsync_WhenCapturedGrantIsRegistered_ConsumesItsExactAuthorizationBeforePublication()
    {
        var store = new InMemorySecurityGrantStore(new FixedTimeProvider());
        var channel = new RecordingQuestionChannel();
        var broker = new DefaultHumanQuestionBroker(store, channel);
        var request = Request(broker.SecurityAudience, captured: true);
        await store.RegisterAsync(request.Grant, TestContext.Current.CancellationToken);

        var result = await broker.AskAsync(request, TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<HumanQuestionAnswered>();
        channel.Prompts.ShouldHaveSingleItem().Id.ShouldBe(request.Id);
    }

    [Fact]
    public async Task AddHumanQuestionBroker_WhenIntentGeneratorIsReplaced_UsesReplacementAndRegistersOneDefault()
    {
        var services = new ServiceCollection();
        var store = new RecordingGrantStore();
        var channel = new RecordingQuestionChannel();
        var intentIds = new FixedSecurityEnforcementIntentIdGenerator(
            new SecurityEnforcementIntentId(Guid.Parse("90000000-0000-0000-0000-000000000002")));
        _ = services.AddSingleton<ISecurityGrantStore>(store);
        _ = services.AddSingleton<IHumanQuestionChannel>(channel);
        _ = services.AddSingleton<IIdentifierGenerator<SecurityEnforcementIntentId>>(intentIds);
        _ = services.AddHumanQuestionBroker().AddHumanQuestionBroker();
        using var provider = services.BuildServiceProvider();
        var broker = provider.GetRequiredService<IHumanQuestionBroker>();

        _ = await broker.AskAsync(Request(broker.SecurityAudience), TestContext.Current.CancellationToken);

        services.Count(descriptor => descriptor.ServiceType == typeof(IHumanQuestionBroker)).ShouldBe(1);
        store.Intents.ShouldHaveSingleItem().Id.ShouldBe(new SecurityEnforcementIntentId(
            Guid.Parse("90000000-0000-0000-0000-000000000002")));
    }

    private static HumanQuestionRequest Request(ComponentId audience, bool captured = false)
    {
        var id = new QuestionId(Guid.Parse("10000000-0000-0000-0000-000000000001"));
        var agentId = new AgentId(Guid.Parse("20000000-0000-0000-0000-000000000002"));
        var sessionId = new SessionId(Guid.Parse("30000000-0000-0000-0000-000000000003"));
        var toolCallId = new ToolCallId(Guid.Parse("40000000-0000-0000-0000-000000000004"));
        var correlation = new InRunOperationCorrelation(
            new OperationId(Guid.Parse("50000000-0000-0000-0000-000000000005")),
            new RunId(Guid.Parse("60000000-0000-0000-0000-000000000006")),
            null);
        var identity = TestSupport.TestExecutionIdentity.Create(
            new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
        ImmutableArray<HumanQuestionOption> options =
        [
            new(new QuestionOptionId("one"), "One", "First option."),
            new(new QuestionOptionId("two"), "Two", "Second option."),
        ];
        var deadline = DateTimeOffset.UnixEpoch.AddMinutes(5);
        var fingerprint = HumanQuestionSecurityBinding.Fingerprint(id, "Choose.", options, false, deadline);
        var scope = new SecurityAuthorizationScope(agentId, sessionId, correlation);
        var policyVersion = new SecurityPolicyVersion(1);
        var authorization = captured
            ? new SecurityAuthorizationContext(
                new SecurityProfileKey("test"),
                new SecurityProfileVersion(1),
                new SecurityPolicySnapshotReference(
                    new SecurityPolicySnapshotId(Guid.Parse("90000000-0000-0000-0000-000000000003")),
                    policyVersion,
                    new ContentHash("sha256:test-policy")),
                new ComponentKey<ISecurityAuthority>("test"),
                new AgentDefinitionRevision(0),
                new ConfigurationVersion(1),
                scope,
                identity)
            : null;
        var grant = authorization is { } context
            ? new SecurityGrant(
                new GrantId(Guid.Parse("70000000-0000-0000-0000-000000000007")),
                new SecurityRequestId(Guid.Parse("80000000-0000-0000-0000-000000000008")),
                scope, identity, context, audience, SecurityOperationKind.StateMutation, SecurityEffect.Create,
                [HumanQuestionSecurityBinding.Resource(id)], fingerprint, policyVersion,
                new SecurityRevocationVersion(1), DateTimeOffset.UnixEpoch, deadline, 1)
            : new SecurityGrant(
                new GrantId(Guid.Parse("70000000-0000-0000-0000-000000000007")),
                new SecurityRequestId(Guid.Parse("80000000-0000-0000-0000-000000000008")),
                scope, identity, audience, SecurityOperationKind.StateMutation, SecurityEffect.Create,
                [HumanQuestionSecurityBinding.Resource(id)], fingerprint, policyVersion,
                new SecurityRevocationVersion(1), DateTimeOffset.UnixEpoch, deadline, 1);
        return new HumanQuestionRequest(id, agentId, sessionId, toolCallId, correlation, identity, "Choose.",
            options, false, deadline, grant);
    }
}
