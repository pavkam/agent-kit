// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network.InMemory.Tests;

internal sealed class TestGrantStore: ISecurityGrantStore
{
    internal GrantConsumptionStatus Status { get; set; } = GrantConsumptionStatus.Consumed;
    internal bool ReturnExactIntentReceipt { get; set; } = true;
    internal Action? OnIntentConsumption { get; set; }
    internal List<SecurityEnforcementRequest> Enforcements { get; } = [];
    internal List<SecurityEnforcementIntent> Intents { get; } = [];

    public ValueTask RegisterAsync(SecurityGrant grant, CancellationToken cancellationToken = default) =>
        ValueTask.CompletedTask;

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
        var receipt = Status is GrantConsumptionStatus.Consumed or GrantConsumptionStatus.Reconciled
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

    public ValueTask<GrantRevocationResult> RevokeAsync(GrantId grantId, RevocationReason reason, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reason);
        return ValueTask.FromResult<GrantRevocationResult>(new GrantRevoked(grantId, reason));
    }
}

internal static class TestSecurity
{
    internal static SecurityGrant CapturedGrant(
        ComponentId audience,
        ImmutableArray<ProtectedResource> resources,
        InputFingerprint fingerprint)
    {
        var scope = Scope();
        var identity = Identity();
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
            scope,
            identity);
        return new SecurityGrant(
            new GrantId(Guid.Parse("12000000-0000-0000-0000-000000000012")),
            new SecurityRequestId(Guid.Parse("13000000-0000-0000-0000-000000000013")),
            scope,
            identity,
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

    internal static SecurityGrant Grant() => CapturedGrant(
        new ComponentId("test.network"),
        [new ProtectedResource(ProtectedResourceKind.NetworkEndpoint, "test")],
        new InputFingerprint("sha256:test"));

    private static SecurityAuthorizationScope Scope() => new(
        new AgentId(Guid.Parse("30000000-0000-0000-0000-000000000003")),
        null,
        new BeforeRunOperationCorrelation(
            new OperationId(Guid.Parse("40000000-0000-0000-0000-000000000004")),
            null));

    private static ExecutionIdentity Identity() => TestSupport.TestExecutionIdentity.Create(
        new TenantId("tenant"),
        new PrincipalId("principal"),
        ExecutionSubjectKind.Human);
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
