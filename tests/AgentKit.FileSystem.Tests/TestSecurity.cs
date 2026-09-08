// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem.Tests;

internal static class TestSecurity
{
    public static ISecurityGrantStore GrantStore() => new AlwaysConsumeGrantStore();

    public static SecurityGrant Grant() => new(
        new GrantId(Guid.NewGuid()),
        new SecurityRequestId(Guid.NewGuid()),
        new SecurityAuthorizationScope(
            new AgentId(Guid.NewGuid()),
            null,
            new BeforeRunOperationCorrelation(new OperationId(Guid.NewGuid()), null)),
        TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human),
        new ComponentId("agentkit.filesystem.sandboxed"),
        SecurityOperationKind.FileRead,
        SecurityEffect.Observe,
        [new ProtectedResource(ProtectedResourceKind.File, "test")],
        new InputFingerprint("sha256:test"),
        new SecurityPolicyVersion(1),
        new SecurityRevocationVersion(1),
        DateTimeOffset.UnixEpoch,
        DateTimeOffset.MaxValue,
        1);

    internal sealed class RecordingGrantStore: ISecurityGrantStore
    {
        public GrantConsumptionResult Result { get; set; } = new(
            GrantConsumptionStatus.Consumed, 0, "Consumed by recording test store.");

        public SecurityEnforcementRequest? LastEnforcement { get; private set; }

        public ValueTask RegisterAsync(SecurityGrant grant, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(
            SecurityGrant grant,
            SecurityEnforcementRequest enforcement,
            CancellationToken cancellationToken = default)
        {
            LastEnforcement = enforcement;
            return ValueTask.FromResult(Result);
        }

        public ValueTask<bool> RevokeAsync(GrantId grantId, CancellationToken cancellationToken = default) => ValueTask.FromResult(true);
    }

    private sealed class AlwaysConsumeGrantStore: ISecurityGrantStore
    {
        public ValueTask RegisterAsync(SecurityGrant grant, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(
            SecurityGrant grant,
            SecurityEnforcementRequest enforcement,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new GrantConsumptionResult(GrantConsumptionStatus.Consumed, 0, "Consumed by test store."));

        public ValueTask<bool> RevokeAsync(GrantId grantId, CancellationToken cancellationToken = default) => ValueTask.FromResult(true);
    }
}
