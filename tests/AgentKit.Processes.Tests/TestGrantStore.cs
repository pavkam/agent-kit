// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Processes.Tests;

internal sealed class TestGrantStore: ISecurityGrantStore
{
    internal GrantConsumptionStatus Status { get; set; } = GrantConsumptionStatus.Consumed;
    internal List<SecurityEnforcementRequest> Enforcements { get; } = [];

    public ValueTask RegisterAsync(SecurityGrant grant, CancellationToken cancellationToken = default) =>
        ValueTask.CompletedTask;

    public ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(
        SecurityGrant grant,
        SecurityEnforcementRequest enforcement,
        CancellationToken cancellationToken = default)
    {
        Enforcements.Add(enforcement);
        return ValueTask.FromResult(new GrantConsumptionResult(Status, 0, Status == GrantConsumptionStatus.Consumed
            ? "Consumed."
            : "Denied."));
    }

    public ValueTask<bool> RevokeAsync(
        GrantId grantId,
        CancellationToken cancellationToken = default) => ValueTask.FromResult(true);

    internal static SecurityGrant Grant() => new(
        new GrantId(Guid.NewGuid()),
        new SecurityRequestId(Guid.NewGuid()),
        new SecurityAuthorizationScope(
            new AgentId(Guid.NewGuid()),
            null,
            new BeforeRunOperationCorrelation(new OperationId(Guid.NewGuid()), null)),
        TestSupport.TestExecutionIdentity.Create(
            new TenantId("tenant"),
            new PrincipalId("principal"),
            ExecutionSubjectKind.Human),
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
