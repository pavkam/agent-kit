// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Tests;

public sealed class DefaultTaskDelegationBrokerTests
{
    [Fact]
    public async Task DelegateAsync_WhenGrantMatches_ConsumesBeforeGrantFreeDispatch()
    {
        var store = new RecordingGrantStore();
        var channel = new RecordingDelegationChannel();
        var broker = new DefaultTaskDelegationBroker(store, channel);
        var request = Request(broker.SecurityAudience);

        var result = await broker.DelegateAsync(request, TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<TaskDelegationChildResult>();
        var enforcement = store.Enforcements.ShouldHaveSingleItem();
        enforcement.Kind.ShouldBe(SecurityOperationKind.Delegation);
        enforcement.Effect.ShouldBe(SecurityEffect.Create);
        enforcement.Resources.ShouldBe([TaskDelegationSecurityBinding.Resource(request.Prompt.Id)]);
        enforcement.InputFingerprint.ShouldBe(request.Grant.InputFingerprint);
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
    public void AddAgentDelegation_WhenCalledTwice_RegistersOneDefaultBroker()
    {
        var services = new ServiceCollection();

        _ = services.AddAgentDelegation().AddAgentDelegation();

        services.Count(descriptor => descriptor.ServiceType == typeof(ITaskDelegationBroker) && descriptor.ImplementationType == typeof(DefaultTaskDelegationBroker)).ShouldBe(1);
    }

    private static TaskDelegationRequest Request(ComponentId audience)
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
            "Implement the parser.",
            ["Tests pass."],
            [new ToolId("read")],
            new TaskDelegationBudget(10, 20),
            DateTimeOffset.UnixEpoch.AddMinutes(5));
        var scope = new SecurityAuthorizationScope(prompt.ParentAgentId, prompt.ParentSessionId, prompt.Correlation);
        var grant = new SecurityGrant(
            new GrantId(Guid.Parse("80000000-0000-0000-0000-000000000008")),
            new SecurityRequestId(Guid.Parse("90000000-0000-0000-0000-000000000009")),
            scope, prompt.Identity, audience, SecurityOperationKind.Delegation, SecurityEffect.Create,
            [TaskDelegationSecurityBinding.Resource(prompt.Id)], TaskDelegationSecurityBinding.Fingerprint(prompt),
            new SecurityPolicyVersion(1), new SecurityRevocationVersion(1), DateTimeOffset.UnixEpoch, prompt.Deadline, 1);
        return new TaskDelegationRequest(prompt, grant);
    }
}
