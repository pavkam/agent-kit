// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network.InMemory.Tests;

internal sealed class TestGrantStore: ISecurityGrantStore
{
    internal GrantConsumptionStatus Status { get; set; } = GrantConsumptionStatus.Consumed;
    internal bool IncludeIntentReceipt { get; set; } = true;
    internal bool ReturnExactIntentReceipt { get; set; } = true;
    internal Action? OnIntentConsumption { get; set; }
    internal int LegacyConsumptionCalls { get; private set; }
    internal List<SecurityEnforcementRequest> Enforcements { get; } = [];
    internal List<SecurityEnforcementIntent> Intents { get; } = [];

    public ValueTask RegisterAsync(SecurityGrant grant, CancellationToken cancellationToken = default) =>
        ValueTask.CompletedTask;

    public ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(
        SecurityGrant grant,
        SecurityEnforcementRequest enforcement,
        CancellationToken cancellationToken = default)
    {
        LegacyConsumptionCalls++;
        Enforcements.Add(enforcement);
        return ValueTask.FromResult(new GrantConsumptionResult(
            Status,
            0,
            Status == GrantConsumptionStatus.Consumed ? "Consumed." : "Denied."));
    }

    public ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(
        SecurityGrant grant,
        SecurityEnforcementRequest enforcement,
        SecurityEnforcementIntent intent,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Enforcements.Add(enforcement);
        Intents.Add(intent);
        OnIntentConsumption?.Invoke();
        var receipt = IncludeIntentReceipt
            && Status is GrantConsumptionStatus.Consumed or GrantConsumptionStatus.Reconciled
            ? new SecurityEnforcementIntentReceipt(
                ReturnExactIntentReceipt
                    ? intent.Id
                    : new SecurityEnforcementIntentId(Guid.Parse("90000000-0000-0000-0000-000000000009")),
                grant.Id,
                grant.RequestId,
                enforcement,
                intent.RequiredFence,
                SecurityEnforcementBinding.Fingerprint(enforcement, intent),
                DateTimeOffset.UnixEpoch)
            : null;
        return ValueTask.FromResult(new GrantConsumptionResult(
            Status,
            0,
            Status == GrantConsumptionStatus.Consumed ? "Consumed." : "Denied.",
            receipt));
    }

    public ValueTask<bool> RevokeAsync(GrantId grantId, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(true);
}

internal static class TestSecurity
{
    internal static SecurityGrant CapturedGrant(
        ComponentId audience,
        ImmutableArray<ProtectedResource> resources,
        InputFingerprint fingerprint)
    {
        var grant = Grant();
        var authorization = new SecurityAuthorizationContext(
            new SecurityProfileKey("test"),
            new SecurityProfileVersion(1),
            new SecurityPolicySnapshotReference(
                new SecurityPolicySnapshotId(Guid.Parse("11000000-0000-0000-0000-000000000011")),
                new SecurityPolicyVersion(1),
                new ContentHash("sha256:test-policy")),
            new ComponentKey<ISecurityAuthority>("test"),
            new AgentDefinitionRevision(0),
            new ConfigurationVersion(1),
            grant.Scope,
            grant.Identity);
        return new SecurityGrant(
            new GrantId(Guid.Parse("12000000-0000-0000-0000-000000000012")),
            new SecurityRequestId(Guid.Parse("13000000-0000-0000-0000-000000000013")),
            grant.Scope,
            grant.Identity,
            authorization,
            audience,
            SecurityOperationKind.Network,
            SecurityEffect.Egress,
            resources,
            fingerprint,
            new SecurityPolicyVersion(1),
            new SecurityRevocationVersion(1),
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.MaxValue,
            1);
    }

    internal static SecurityGrant Grant() => new(
        new GrantId(Guid.Parse("10000000-0000-0000-0000-000000000001")),
        new SecurityRequestId(Guid.Parse("20000000-0000-0000-0000-000000000002")),
        new SecurityAuthorizationScope(
            new AgentId(Guid.Parse("30000000-0000-0000-0000-000000000003")),
            null,
            new BeforeRunOperationCorrelation(
                new OperationId(Guid.Parse("40000000-0000-0000-0000-000000000004")),
                null)),
        TestSupport.TestExecutionIdentity.Create(
            new TenantId("tenant"),
            new PrincipalId("principal"),
            ExecutionSubjectKind.Human),
        new ComponentId("test.network"),
        SecurityOperationKind.Network,
        SecurityEffect.Egress,
        [new ProtectedResource(ProtectedResourceKind.NetworkEndpoint, "test")],
        new InputFingerprint("sha256:test"),
        new SecurityPolicyVersion(1),
        new SecurityRevocationVersion(1),
        DateTimeOffset.UnixEpoch,
        DateTimeOffset.MaxValue,
        1);
}

internal sealed class FixedTimeProvider: TimeProvider
{
    public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch;
}

internal sealed class SequenceSecurityEnforcementIntentIdGenerator(params Guid[] values)
    : IIdentifierGenerator<SecurityEnforcementIntentId>
{
    private readonly Queue<Guid> _values = new(values);

    public SecurityEnforcementIntentId Create() => new(_values.Dequeue());
}
