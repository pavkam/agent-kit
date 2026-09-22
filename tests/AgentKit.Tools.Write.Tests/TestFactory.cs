// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Write.Tests;

using AgentKit.TestSupport;

/// <summary>Provides construction helpers for write-file tool tests.</summary>
internal static class TestFactory
{
    [Obsolete]
    public static WriteFileTool Tool(IFileSystem? fileSystem = null, ISecurityAuthority? authority = null) => new(
        fileSystem ?? new FakeFileSystem(),
        new FixedSecurityAuthoritySelector(authority ?? new AllowingSecurityAuthority()),
        new SecurityRequestIdGenerator(),
        TimeProvider.System);

    public static ISecurityGrantStore GrantStore() => new AlwaysConsumeGrantStore();

    public static ISecurityAuthority DenyingAuthority() => new DenyingSecurityAuthority();

    public static ExecutionIdentity Identity() =>
        TestExecutionIdentity.Create(new TenantId("tenant-1"), new PrincipalId("user-1"), ExecutionSubjectKind.Human);

    public static OperationCorrelation Correlation() =>
        new InRunOperationCorrelation(new OperationId(Guid.NewGuid()), new RunId(Guid.NewGuid()), null);

    public static ToolExecutionContext ExecutionContext()
    {
        var agentId = new AgentId(Guid.NewGuid());
        var sessionId = new SessionId(Guid.NewGuid());
        var correlation = Correlation();
        var identity = Identity();
        var scope = new SecurityAuthorizationScope(agentId, sessionId, correlation);
        return new ToolExecutionContext(
            agentId,
            sessionId,
            new ToolCallId(Guid.NewGuid()),
            correlation,
            identity,
            Authorization(scope, identity),
            null);
    }

    public static ToolInvocationRequest Request(string json) => new(
        ExecutionContext(), JsonDocument.Parse(json).RootElement, DateTimeOffset.UnixEpoch);

    public static string ReadText(ToolInvocationResult result) => ((TextPart) result.Content[0]).Text;

    private static SecurityAuthorizationContext Authorization(SecurityAuthorizationScope scope, ExecutionIdentity identity) => new(
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

    public static IServiceCollection AddSecurityDependencies(IServiceCollection services) => services
        .AddSingleton<ISecurityAuthority, AllowingSecurityAuthority>()
        .AddSingleton<ISecurityAuthoritySelector>(sp => new FixedSecurityAuthoritySelector(sp.GetRequiredService<ISecurityAuthority>()))
        .AddSingleton<IIdentifierGenerator<SecurityRequestId>, SecurityRequestIdGenerator>()
        .AddSingleton(TimeProvider.System);

    private sealed class AllowingSecurityAuthority: ISecurityAuthority
    {
        public ValueTask<SecurityDecision> AuthorizeAsync(SecurityRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();
            var now = DateTimeOffset.UtcNow;
            SecurityDecision decision = new SecurityAllowed(
                request.Id,
                new SecurityPolicyVersion(1),
                new SecurityGrant(
                    new GrantId(Guid.NewGuid()),
                    request.Id,
                    request.Scope,
                    request.Identity,
                    request.Audience,
                    request.Kind,
                    request.Effect,
                    request.Resources,
                    request.InputFingerprint,
                    new SecurityPolicyVersion(1),
                    new SecurityRevocationVersion(1),
                    now.AddMinutes(-1),
                    now.AddMinutes(1),
                    1));
            return ValueTask.FromResult(decision);
        }
    }

    private sealed class SecurityRequestIdGenerator: IIdentifierGenerator<SecurityRequestId>
    {
        public SecurityRequestId Create() => new(Guid.NewGuid());
    }

    private sealed class DenyingSecurityAuthority: ISecurityAuthority
    {
        public ValueTask<SecurityDecision> AuthorizeAsync(SecurityRequest request, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<SecurityDecision>(new SecurityDenied(
                request.Id, new SecurityPolicyVersion(1), new SecurityDenial("test.denied", "Denied by test policy.")));
    }

    private sealed class AlwaysConsumeGrantStore: ISecurityGrantStore
    {
        public ValueTask RegisterAsync(SecurityGrant grant, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(
            SecurityGrant grant,
            SecurityEnforcementRequest enforcement,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new GrantConsumptionResult(GrantConsumptionStatus.Consumed, 0, "Consumed by test store."));

        public ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(
            SecurityGrant grant,
            SecurityEnforcementRequest enforcement,
            SecurityEnforcementIntent intent,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var receipt = new SecurityEnforcementIntentReceipt(
                intent.Id,
                grant.Id,
                grant.RequestId,
                enforcement,
                intent.RequiredFence,
                SecurityEnforcementBinding.Fingerprint(enforcement, intent),
                DateTimeOffset.UnixEpoch);
            return ValueTask.FromResult(new GrantConsumptionResult(
                GrantConsumptionStatus.Consumed,
                0,
                "Consumed by test store.",
                receipt));
        }

        public ValueTask<GrantRevocationResult> RevokeAsync(GrantId grantId, RevocationReason reason, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(reason);
            return ValueTask.FromResult<GrantRevocationResult>(new GrantRevoked(grantId, reason));
        }
    }
}
