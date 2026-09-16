// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Security;

using AgentKit;
using AgentKit.TestSupport;

/// <summary>
/// Deterministic builders for security contract values shared across the
/// Security fixture files. Every identity is a fixed GUID so equality
/// assertions never depend on generation order.
/// </summary>
internal static class SecurityAbstractionsTestData
{
    public static AgentId AgentId { get; } = new(Guid.Parse("f0000000-0000-0000-0000-000000000001"));
    public static SessionId SessionId { get; } = new(Guid.Parse("f0000000-0000-0000-0000-000000000002"));
    public static OperationId OperationId { get; } = new(Guid.Parse("f0000000-0000-0000-0000-000000000003"));

    public static ExecutionIdentity Identity() =>
        TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);

    public static BeforeRunOperationCorrelation Correlation() => new(OperationId, null);

    public static SecurityAuthorizationScope Scope() => new(AgentId, SessionId, Correlation());

    public static SecurityAuthorizationContext Authorization(SecurityAuthorizationScope? scope = null, ExecutionIdentity? identity = null) =>
        new(
            new SecurityProfileKey("default"),
            new SecurityProfileVersion(1),
            new SecurityPolicySnapshotReference(
                new SecurityPolicySnapshotId(Guid.Parse("f0000000-0000-0000-0000-000000000004")),
                new SecurityPolicyVersion(1),
                new ContentHash("sha256:policy")),
            new ComponentKey<ISecurityAuthority>("authority"),
            new AgentDefinitionRevision(1),
            new ConfigurationVersion(1),
            scope ?? Scope(),
            identity ?? Identity());

    public static ProtectedResource Resource() => new(ProtectedResourceKind.ApplicationState, "session:test");

    public static SecurityRequest Request() =>
        new(new SecurityRequestId(Guid.Parse("f0000000-0000-0000-0000-000000000005")), Scope(), null, Identity(),
            Authorization(), new ComponentId("session"), SecurityOperationKind.StateRead, SecurityEffect.Observe,
            [Resource()], new InputFingerprint("sha256:input"), DateTimeOffset.UnixEpoch.AddMinutes(1));

    public static SecurityGrant Grant() =>
        new(
            new GrantId(Guid.Parse("f0000000-0000-0000-0000-000000000006")),
            new SecurityRequestId(Guid.Parse("f0000000-0000-0000-0000-000000000007")),
            Scope(),
            Identity(),
            Authorization(),
            new ComponentId("session"),
            SecurityOperationKind.StateRead,
            SecurityEffect.Observe,
            [Resource()],
            new InputFingerprint("sha256:input"),
            new SecurityPolicyVersion(1),
            new SecurityRevocationVersion(1),
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch.AddMinutes(1),
            1);
}
