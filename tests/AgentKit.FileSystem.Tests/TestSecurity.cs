// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem.Tests;

internal static class TestSecurity
{
    public static ISecurityGrantStore GrantStore() => new AlwaysConsumeGrantStore();

    public static SecurityGrant Grant()
    {
        var __scope = new SecurityAuthorizationScope(
            new AgentId(Guid.NewGuid()),
            null,
            new BeforeRunOperationCorrelation(new OperationId(Guid.NewGuid()), null));
        var __identity = TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
        return new(
        new GrantId(Guid.NewGuid()),
        new SecurityRequestId(Guid.NewGuid()),
        __scope,
        __identity,
        TestSecurityEvidence.Authorization(__scope.AgentId, __scope.SessionId, __scope.Correlation, __identity),
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
    }

    internal static SecurityGrant CapturedGrant(
        ComponentId audience,
        SecurityOperationKind kind,
        SecurityEffect effect,
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
            kind,
            effect,
            resources,
            fingerprint,
            new SecurityPolicyVersion(1),
            new SecurityRevocationVersion(1),
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.MaxValue,
            1);
    }

    internal sealed class RecordingGrantStore: ISecurityGrantStore
    {
        /// <summary>Gets or sets a non-consumption result every attempt reports, or null to consume with a matching receipt.</summary>
        public GrantConsumptionResult? Result { get; set; }

        public SecurityEnforcementRequest? LastEnforcement { get; private set; }

        public SecurityEnforcementIntent? LastIntent { get; private set; }

        public Action? OnIntentConsumption { get; set; }

        public bool ReturnExactIntentReceipt { get; set; } = true;

        public ValueTask RegisterAsync(SecurityGrant grant, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(
            SecurityGrant grant,
            SecurityEnforcementRequest enforcement,
            SecurityEnforcementIntent intent,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastEnforcement = enforcement;
            LastIntent = intent;
            OnIntentConsumption?.Invoke();
            if (Result is { } configured)
            {
                return ValueTask.FromResult(configured);
            }

            var receipt = new SecurityEnforcementIntentReceipt(
                ReturnExactIntentReceipt
                    ? intent.Id
                    : new SecurityEnforcementIntentId(Guid.Parse("90000000-0000-0000-0000-000000000009")),
                grant.Id,
                grant.RequestId,
                enforcement,
                intent.RequiredFence,
                SecurityEnforcementBinding.Fingerprint(enforcement, intent),
                DateTimeOffset.UnixEpoch);
            return ValueTask.FromResult(new GrantConsumptionResult(
                GrantConsumptionStatus.Consumed,
                0,
                "Consumed by recording test store.",
                receipt));
        }

        public ValueTask<GrantRevocationResult> RevokeAsync(GrantId grantId, RevocationReason reason, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(reason);
            return ValueTask.FromResult<GrantRevocationResult>(new GrantRevoked(grantId, reason));
        }
    }

    private sealed class AlwaysConsumeGrantStore: ISecurityGrantStore
    {
        public ValueTask RegisterAsync(SecurityGrant grant, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(
            SecurityGrant grant,
            SecurityEnforcementRequest enforcement,
            SecurityEnforcementIntent intent,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new GrantConsumptionResult(
                GrantConsumptionStatus.Consumed,
                0,
                "Consumed by test store.",
                new SecurityEnforcementIntentReceipt(
                    intent.Id,
                    grant.Id,
                    grant.RequestId,
                    enforcement,
                    intent.RequiredFence,
                    SecurityEnforcementBinding.Fingerprint(enforcement, intent),
                    DateTimeOffset.UnixEpoch)));

        public ValueTask<GrantRevocationResult> RevokeAsync(GrantId grantId, RevocationReason reason, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(reason);
            return ValueTask.FromResult<GrantRevocationResult>(new GrantRevoked(grantId, reason));
        }
    }
}

internal sealed class SequenceSecurityEnforcementIntentIdGenerator(params Guid[] values)
    : IIdentifierGenerator<SecurityEnforcementIntentId>
{
    private readonly Queue<Guid> _values = new(values);

    public SecurityEnforcementIntentId Create() => new(_values.Dequeue());
}
