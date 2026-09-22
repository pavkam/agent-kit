// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Processes.Tests;

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
        return ValueTask.FromResult(new GrantConsumptionResult(Status, 0, Status == GrantConsumptionStatus.Consumed
            ? "Consumed."
            : "Denied."));
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

    internal static SecurityGrant Grant()
    {
        var scope = new SecurityAuthorizationScope(
            new AgentId(Guid.NewGuid()),
            null,
            new BeforeRunOperationCorrelation(new OperationId(Guid.NewGuid()), null));
        var identity = TestExecutionIdentity.Create(
            new TenantId("tenant"),
            new PrincipalId("principal"),
            ExecutionSubjectKind.Human);
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
            new GrantId(Guid.NewGuid()),
            new SecurityRequestId(Guid.NewGuid()),
            scope,
            identity,
            authorization,
            new ComponentId("agentkit.processes.operating-system"),
            SecurityOperationKind.Process,
            SecurityEffect.Execute,
            [new ProtectedResource(ProtectedResourceKind.Process, "test")],
            new InputFingerprint("sha256:test"),
            new SecurityPolicyVersion(1),
            new SecurityRevocationVersion(1),
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.MaxValue,
            1);
    }
    public ValueTask<GrantRevocationResult> RevokeAsync(GrantId grantId, RevocationReason reason, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult<GrantRevocationResult>(new GrantRevoked(grantId, reason));
}

internal sealed class SequenceSecurityEnforcementIntentIdGenerator(params Guid[] values)
    : IIdentifierGenerator<SecurityEnforcementIntentId>
{
    private readonly Queue<Guid> _values = new(values);

    public SecurityEnforcementIntentId Create() => new(_values.Dequeue());
}

internal static class CapturedGrantFactory
{
    internal static SecurityGrant Create(
        ComponentId audience,
        ImmutableArray<ProtectedResource> resources,
        InputFingerprint fingerprint)
    {
        var grant = TestGrantStore.Grant();
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
            SecurityOperationKind.Process,
            SecurityEffect.Execute,
            resources,
            fingerprint,
            new SecurityPolicyVersion(1),
            new SecurityRevocationVersion(1),
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.MaxValue,
            1);
    }
}
