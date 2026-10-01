// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.LanguageServices.Scripted.Tests;

internal sealed class TestGrantStore: ISecurityGrantStore
{
    internal GrantConsumptionStatus Status { get; set; } = GrantConsumptionStatus.Consumed;
    internal List<SecurityEnforcementRequest> Enforcements { get; } = [];
    internal List<SecurityEnforcementIntent> Intents { get; } = [];
    internal bool ReturnExactReceipt { get; set; } = true;
    internal Action? OnConsume { get; set; }

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
        OnConsume?.Invoke();
        var receipt = (Status is GrantConsumptionStatus.Consumed or GrantConsumptionStatus.Reconciled)
            ? new SecurityEnforcementIntentReceipt(
                ReturnExactReceipt
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

    internal static SecurityGrant Grant()
    {
        var scope = new SecurityAuthorizationScope(
            new AgentId(Guid.Parse("30000000-0000-0000-0000-000000000003")),
            null,
            new BeforeRunOperationCorrelation(
                new OperationId(Guid.Parse("40000000-0000-0000-0000-000000000004")),
                null));
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
            new GrantId(Guid.Parse("10000000-0000-0000-0000-000000000001")),
            new SecurityRequestId(Guid.Parse("20000000-0000-0000-0000-000000000002")),
            scope,
            identity,
            authorization,
            new ComponentId("agentkit.language.scripted"),
            SecurityOperationKind.FileRead,
            SecurityEffect.Observe,
            [new ProtectedResource(ProtectedResourceKind.File, "src/a.cs")],
            new InputFingerprint("sha256:scripted"),
            new SecurityPolicyVersion(1),
            new SecurityRevocationVersion(1),
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.MaxValue,
            1);
    }

    internal static SecurityGrant CapturedGrant(
        LanguageQueryId queryId,
        LanguageQueryKind kind,
        FileSystemPath? path,
        LanguagePosition? position,
        string? query,
        int maximumResults,
        TimeSpan timeout)
    {
        var grant = Grant();
        var authorization = grant.Authorization;
        return new SecurityGrant(
            grant.Id,
            grant.RequestId,
            grant.Scope,
            grant.Identity,
            authorization,
            grant.Audience,
            SecurityOperationKind.FileRead,
            SecurityEffect.Observe,
            [LanguageSecurityBinding.Resource(kind, path)],
            LanguageSecurityBinding.Fingerprint(queryId, kind, path, position, query, maximumResults, timeout),
            grant.PolicyVersion,
            grant.RevocationVersion,
            grant.NotBefore,
            grant.ExpiresAt,
            grant.AllowedUses);
    }
}
