// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Security;



/// <summary>Verifies SecurityPolicyVersion behavior and contracts.</summary>
public sealed class SecurityPolicyVersionTests: Conformance.LongIdentityConformanceTests<SecurityPolicyVersion>
{
    [Fact]
    public void GrantConstructor_WhenCapturedPolicyVersionDiffers_ThrowsArgumentExceptionForAuthorization()
    {
        var scope = Scope();
        var identity = Identity();
        var authorization = Authorization(scope, identity);
        var exception = Should.Throw<ArgumentException>(() => Grant(scope, identity, authorization, new SecurityPolicyVersion(2)));
        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("authorization");
    }

    [Fact]
    public void GrantWith_WhenCapturedPolicyVersionDiffers_ThrowsArgumentExceptionForPolicyVersion()
    {
        var scope = Scope();
        var identity = Identity();
        var authorization = Authorization(scope, identity);
        var exception = Should.Throw<ArgumentException>(() => _ = Grant(scope, identity, authorization) with { PolicyVersion = new SecurityPolicyVersion(2) });
        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("PolicyVersion");
    }

    private static SecurityGrant Grant(SecurityAuthorizationScope scope, ExecutionIdentity identity, SecurityAuthorizationContext authorization, SecurityPolicyVersion? policyVersion = null) => new(new GrantId(Guid.NewGuid()), new SecurityRequestId(Guid.NewGuid()), scope, identity, authorization, new ComponentId("session"), SecurityOperationKind.StateRead, SecurityEffect.Observe, [Resource()], new InputFingerprint("sha256:input"), policyVersion ?? new SecurityPolicyVersion(1), new SecurityRevocationVersion(1), DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(1), 1);
    private static SecurityAuthorizationContext Authorization(SecurityAuthorizationScope scope, ExecutionIdentity identity) => new(new SecurityProfileKey("default"), new SecurityProfileVersion(1), new SecurityPolicySnapshotReference(new SecurityPolicySnapshotId(Guid.NewGuid()), new SecurityPolicyVersion(1), new ContentHash("sha256:policy")), new ComponentKey<ISecurityAuthority>("authority"), new AgentDefinitionRevision(1), new ConfigurationVersion(1), scope, identity);
    private static SecurityAuthorizationScope Scope() => new(new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid()), new BeforeRunOperationCorrelation(new OperationId(Guid.NewGuid()), null));
    private static ExecutionIdentity Identity(string principal = "principal") => TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId(principal), ExecutionSubjectKind.Human);
    private static ProtectedResource Resource() => new(ProtectedResourceKind.ApplicationState, "session:test");

    /// <inheritdoc/>
    protected override SecurityPolicyVersion Create(long value) => new(value);

    /// <inheritdoc/>
    protected override long GetValue(SecurityPolicyVersion subject) => subject.Value;

    /// <inheritdoc/>
    protected override bool RequiresPositiveValue => true;
}
