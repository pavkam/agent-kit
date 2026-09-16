// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Security;



/// <summary>Verifies SecurityAuthorizationContext behavior and contracts.</summary>
public sealed class SecurityAuthorizationContextTests
{
    /// <summary>Verifies an authorization context rejects every nullable or default captured dependency.</summary>
    [Fact]
    public void Constructor_WhenAuthorizationContextPartIsInvalid_ThrowsExactArgument()
    {
        Should.Throw<ArgumentNullException>(() => NewContext(default, new SecurityProfileVersion(2), Reference(), new ComponentKey<ISecurityAuthority>("authority"), new ConfigurationVersion(9), Scope(), Identity())).ParamName.ShouldBe("profileKey");
        Should.Throw<ArgumentOutOfRangeException>(() => NewContext(new SecurityProfileKey("default"), default, Reference(), new ComponentKey<ISecurityAuthority>("authority"), new ConfigurationVersion(9), Scope(), Identity())).ParamName.ShouldBe("profileVersion");
        Should.Throw<ArgumentNullException>(() => NewContext(new SecurityProfileKey("default"), new SecurityProfileVersion(2), null!, new ComponentKey<ISecurityAuthority>("authority"), new ConfigurationVersion(9), Scope(), Identity())).ParamName.ShouldBe("policySnapshot");
        Should.Throw<ArgumentNullException>(() => NewContext(new SecurityProfileKey("default"), new SecurityProfileVersion(2), Reference(), default, new ConfigurationVersion(9), Scope(), Identity())).ParamName.ShouldBe("authorityKey");
        Should.Throw<ArgumentOutOfRangeException>(() => NewContext(new SecurityProfileKey("default"), new SecurityProfileVersion(2), Reference(), new ComponentKey<ISecurityAuthority>("authority"), default, Scope(), Identity())).ParamName.ShouldBe("configurationVersion");
        Should.Throw<ArgumentNullException>(() => NewContext(new SecurityProfileKey("default"), new SecurityProfileVersion(2), Reference(), new ComponentKey<ISecurityAuthority>("authority"), new ConfigurationVersion(9), null!, Identity())).ParamName.ShouldBe("scope");
        Should.Throw<ArgumentNullException>(() => NewContext(new SecurityProfileKey("default"), new SecurityProfileVersion(2), Reference(), new ComponentKey<ISecurityAuthority>("authority"), new ConfigurationVersion(9), Scope(), null!)).ParamName.ShouldBe("identity");
    }

    /// <summary>Verifies snapshot aggregates have structural equality and get-only public state.</summary>
    [Fact]
    public void Equality_WhenCapturedValuesMatch_IsStructuralAndImmutable()
    {
        var first = Context();
        var equal = Context();
        var different = NewContext(new SecurityProfileKey("default"), new SecurityProfileVersion(2), Reference(), new ComponentKey<ISecurityAuthority>("authority"), new ConfigurationVersion(10), Scope(), Identity());
        first.ShouldBe(equal);
        first.ShouldNotBe(different);
        typeof(SecurityAuthorizationContext).GetProperties().ShouldAllBe(property => property.SetMethod == null);
    }

    private static SecurityAuthorizationContext Context() => NewContext(new SecurityProfileKey("default"), new SecurityProfileVersion(2), Reference(), new ComponentKey<ISecurityAuthority>("authority"), new ConfigurationVersion(9), Scope(), Identity());

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = Context();
        var copy = original with { };
        copy.ShouldBe(original);
    }
    private static SecurityAuthorizationContext NewContext(SecurityProfileKey profileKey, SecurityProfileVersion profileVersion, SecurityPolicySnapshotReference policySnapshot, ComponentKey<ISecurityAuthority> authorityKey, ConfigurationVersion configurationVersion, SecurityAuthorizationScope scope, ExecutionIdentity identity) => new(profileKey, profileVersion, policySnapshot, authorityKey, new AgentDefinitionRevision(0), configurationVersion, scope, identity);
    private static SecurityPolicySnapshotReference Reference() => new(SnapshotId(), PolicyVersion(), Hash());
    private static SecurityPolicySnapshotId SnapshotId() => new(Guid.Parse("11111111-1111-1111-1111-111111111111"));
    private static SecurityPolicyVersion PolicyVersion() => new(3);
    private static ContentHash Hash() => new("sha256:test");
    private static AgentId AgentId() => new(Guid.Parse("22222222-2222-2222-2222-222222222222"));
    private static SessionId SessionId() => new(Guid.Parse("33333333-3333-3333-3333-333333333333"));
    private static OperationId OperationId() => new(Guid.Parse("44444444-4444-4444-4444-444444444444"));
    private static BeforeRunOperationCorrelation Correlation() => new(OperationId(), null);
    private static SecurityAuthorizationScope Scope() => new(AgentId(), SessionId(), Correlation());
    private static ExecutionIdentity Identity() => TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
}
