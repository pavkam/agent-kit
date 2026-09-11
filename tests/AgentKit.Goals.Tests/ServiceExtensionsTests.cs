// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Tests;



/// <summary>Verifies ServiceExtensions behavior and contracts.</summary>
public sealed class ServiceExtensionsTests
{
    [Fact]
    public async Task AddAgentDelegation_WhenIntentGeneratorIsReplaced_UsesReplacementAndRegistersOneDefaultBroker()
    {
        var services = new ServiceCollection();
        var store = new RecordingGrantStore();
        var channel = new RecordingDelegationChannel();
        var intentId = new SecurityEnforcementIntentId(Guid.Parse("e0000000-0000-0000-0000-00000000000e"));
        _ = services.AddSingleton<ISecurityGrantStore>(store);
        _ = services.AddSingleton<ITaskDelegationChannel>(channel);
        _ = services.AddSingleton<IIdentifierGenerator<SecurityEnforcementIntentId>>(new FixedSecurityEnforcementIntentIdGenerator(intentId));
        _ = services.AddAgentDelegation().AddAgentDelegation();
        using var provider = services.BuildServiceProvider();
        var broker = provider.GetRequiredService<ITaskDelegationBroker>();
        _ = await broker.DelegateAsync(Request(broker.SecurityAudience), TestContext.Current.CancellationToken);
        services.Count(descriptor => descriptor.ServiceType == typeof(ITaskDelegationBroker)).ShouldBe(1);
        store.Intents.ShouldHaveSingleItem().Id.ShouldBe(intentId);
    }

    [Fact]
    public async Task AddAgentDelegation_WhenObservationDependenciesAreReplaced_UsesBoth()
    {
        var services = new ServiceCollection();
        var store = new RecordingGrantStore();
        var channel = new RecordingDelegationChannel();
        var timeProvider = new CountingTimeProvider();
        var logger = new RecordingDelegationLogger();
        _ = services.AddSingleton<ISecurityGrantStore>(store);
        _ = services.AddSingleton<ITaskDelegationChannel>(channel);
        _ = services.AddSingleton<TimeProvider>(timeProvider);
        _ = services.AddSingleton<ILogger<DefaultTaskDelegationBroker>>(logger);
        _ = services.AddAgentDelegation();
        using var provider = services.BuildServiceProvider();
        var broker = provider.GetRequiredService<ITaskDelegationBroker>();
        _ = await broker.DelegateAsync(Request(broker.SecurityAudience), TestContext.Current.CancellationToken);
        timeProvider.TimestampCalls.ShouldBe(2);
        logger.Events.ShouldHaveSingleItem().ShouldBe((2000, LogLevel.Information));
        logger.FieldNames.ShouldHaveSingleItem().ShouldBe(["DelegationId", "TenantId", "AgentId", "SessionId", "RunId", "TurnId", "ToolCallId", "OperationId", "SecurityRequestId", "Outcome", "{OriginalFormat}"]);
    }

    private static TaskDelegationRequest Request(ComponentId audience, bool captured = false, TurnId? turnId = null)
    {
        var prompt = new TaskDelegationPrompt(new DelegationId(Guid.Parse("10000000-0000-0000-0000-000000000001")), new AgentId(Guid.Parse("20000000-0000-0000-0000-000000000002")), new SessionId(Guid.Parse("30000000-0000-0000-0000-000000000003")), new RunId(Guid.Parse("40000000-0000-0000-0000-000000000004")), new InRunOperationCorrelation(new OperationId(Guid.Parse("50000000-0000-0000-0000-000000000005")), new RunId(Guid.Parse("40000000-0000-0000-0000-000000000004")), turnId), new ToolCallId(Guid.Parse("60000000-0000-0000-0000-000000000006")), TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human), new AgentId(Guid.Parse("70000000-0000-0000-0000-000000000007")), "Implement the parser.", ["Tests pass."], [new ToolId("read")], new TaskDelegationBudget(10, 20), DateTimeOffset.UnixEpoch.AddMinutes(5));
        var scope = new SecurityAuthorizationScope(prompt.ParentAgentId, prompt.ParentSessionId, prompt.Correlation);
        var policyVersion = new SecurityPolicyVersion(1);
        var authorization = captured ? new SecurityAuthorizationContext(new SecurityProfileKey("test"), new SecurityProfileVersion(1), new SecurityPolicySnapshotReference(new SecurityPolicySnapshotId(Guid.Parse("d0000000-0000-0000-0000-00000000000d")), policyVersion, new ContentHash("sha256:test-policy")), new ComponentKey<ISecurityAuthority>("test"), new AgentDefinitionRevision(0), new ConfigurationVersion(1), scope, prompt.Identity) : null;
        var grant = authorization is { } context ? new SecurityGrant(new GrantId(Guid.Parse("80000000-0000-0000-0000-000000000008")), new SecurityRequestId(Guid.Parse("90000000-0000-0000-0000-000000000009")), scope, prompt.Identity, context, audience, SecurityOperationKind.Delegation, SecurityEffect.Create, [TaskDelegationSecurityBinding.Resource(prompt.Id)], TaskDelegationSecurityBinding.Fingerprint(prompt), policyVersion, new SecurityRevocationVersion(1), DateTimeOffset.UnixEpoch, prompt.Deadline, 1) : new SecurityGrant(new GrantId(Guid.Parse("80000000-0000-0000-0000-000000000008")), new SecurityRequestId(Guid.Parse("90000000-0000-0000-0000-000000000009")), scope, prompt.Identity, audience, SecurityOperationKind.Delegation, SecurityEffect.Create, [TaskDelegationSecurityBinding.Resource(prompt.Id)], TaskDelegationSecurityBinding.Fingerprint(prompt), policyVersion, new SecurityRevocationVersion(1), DateTimeOffset.UnixEpoch, prompt.Deadline, 1);
        return new TaskDelegationRequest(prompt, grant);
    }
}
