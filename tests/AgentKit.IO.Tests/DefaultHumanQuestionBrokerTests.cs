// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO.Tests;

public sealed class DefaultHumanQuestionBrokerTests
{
    [Fact]
    public async Task AskAsync_WhenGrantMatches_ConsumesBeforePublishingGrantFreePrompt()
    {
        var store = new RecordingGrantStore();
        var channel = new RecordingQuestionChannel();
        var broker = new DefaultHumanQuestionBroker(store, channel);
        var request = Request(broker.SecurityAudience);

        var result = await broker.AskAsync(request, TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<HumanQuestionAnswered>();
        var enforcement = store.Enforcements.ShouldHaveSingleItem();
        enforcement.Audience.ShouldBe(broker.SecurityAudience);
        enforcement.Resources.ShouldBe([HumanQuestionSecurityBinding.Resource(request.Id)]);
        enforcement.InputFingerprint.ShouldBe(request.Grant.InputFingerprint);
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
    public void AddHumanQuestionBroker_WhenCalledTwice_RegistersOneDefault()
    {
        var services = new ServiceCollection();

        _ = services.AddHumanQuestionBroker().AddHumanQuestionBroker();

        services.Count(descriptor => descriptor.ServiceType == typeof(IHumanQuestionBroker)
            && descriptor.ImplementationType == typeof(DefaultHumanQuestionBroker)).ShouldBe(1);
    }

    private static HumanQuestionRequest Request(ComponentId audience)
    {
        var id = new QuestionId(Guid.Parse("10000000-0000-0000-0000-000000000001"));
        var agentId = new AgentId(Guid.Parse("20000000-0000-0000-0000-000000000002"));
        var sessionId = new SessionId(Guid.Parse("30000000-0000-0000-0000-000000000003"));
        var toolCallId = new ToolCallId(Guid.Parse("40000000-0000-0000-0000-000000000004"));
        var correlation = new InRunOperationCorrelation(
            new OperationId(Guid.Parse("50000000-0000-0000-0000-000000000005")),
            new RunId(Guid.Parse("60000000-0000-0000-0000-000000000006")),
            null);
        var identity = new ExecutionIdentity(
            new TenantId("tenant"),
            new PrincipalId("principal"),
            ExecutionSubjectKind.Human,
            ExtensionData.Empty);
        ImmutableArray<HumanQuestionOption> options =
        [
            new(new QuestionOptionId("one"), "One", "First option."),
            new(new QuestionOptionId("two"), "Two", "Second option."),
        ];
        var deadline = DateTimeOffset.UnixEpoch.AddMinutes(5);
        var fingerprint = HumanQuestionSecurityBinding.Fingerprint(id, "Choose.", options, false, deadline);
        var scope = new SecurityAuthorizationScope(agentId, sessionId, correlation);
        var grant = new SecurityGrant(
            new GrantId(Guid.Parse("70000000-0000-0000-0000-000000000007")),
            new SecurityRequestId(Guid.Parse("80000000-0000-0000-0000-000000000008")),
            scope,
            identity,
            audience,
            SecurityOperationKind.StateMutation,
            SecurityEffect.Create,
            [HumanQuestionSecurityBinding.Resource(id)],
            fingerprint,
            new SecurityPolicyVersion(1),
            new SecurityRevocationVersion(1),
            DateTimeOffset.UnixEpoch,
            deadline,
            1);
        return new HumanQuestionRequest(
            id,
            agentId,
            sessionId,
            toolCallId,
            correlation,
            identity,
            "Choose.",
            options,
            false,
            deadline,
            grant);
    }
}
