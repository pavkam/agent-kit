// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network.Tests;

internal sealed class TestGrantStore: ISecurityGrantStore
{
    internal GrantConsumptionStatus Status { get; set; } = GrantConsumptionStatus.Consumed;
    internal List<SecurityEnforcementRequest> Enforcements { get; } = [];

    public ValueTask RegisterAsync(SecurityGrant grant, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

    public ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(
        SecurityGrant grant,
        SecurityEnforcementRequest enforcement,
        CancellationToken cancellationToken = default)
    {
        Enforcements.Add(enforcement);
        return ValueTask.FromResult(new GrantConsumptionResult(
            Status,
            0,
            Status == GrantConsumptionStatus.Consumed ? "Consumed." : "Denied."));
    }

    public ValueTask<bool> RevokeAsync(GrantId grantId, CancellationToken cancellationToken = default) => ValueTask.FromResult(true);
}

internal static class TestSecurity
{
    internal static SecurityGrant Grant() => new(
        new GrantId(Guid.Parse("10000000-0000-0000-0000-000000000001")),
        new SecurityRequestId(Guid.Parse("20000000-0000-0000-0000-000000000002")),
        new SecurityAuthorizationScope(
            new AgentId(Guid.Parse("30000000-0000-0000-0000-000000000003")),
            null,
            new BeforeRunOperationCorrelation(
                new OperationId(Guid.Parse("40000000-0000-0000-0000-000000000004")),
                null)),
        new ExecutionIdentity(
            new TenantId("tenant"),
            new PrincipalId("principal"),
            ExecutionSubjectKind.Human,
            ExtensionData.Empty),
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
