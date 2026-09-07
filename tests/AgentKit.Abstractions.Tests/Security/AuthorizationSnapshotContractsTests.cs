// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Security;

/// <summary>Verifies immutable authorization-snapshot values reject fabricated or incomplete evidence.</summary>
public sealed class AuthorizationSnapshotContractsTests
{
    /// <summary>Verifies snapshot identifiers reject the default GUID with the exact parameter name.</summary>
    [Fact]
    public void Constructor_WhenSnapshotIdIsEmpty_ThrowsExactArgument()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new SecurityPolicySnapshotId(Guid.Empty));

        exception.ParamName.ShouldBe("value");
    }

    /// <summary>Verifies published profile and configuration versions must be positive.</summary>
    /// <param name="value">The invalid version value.</param>
    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void Constructor_WhenPublishedVersionIsNotPositive_ThrowsExactArgument(long value)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new SecurityProfileVersion(value)).ParamName.ShouldBe("value");
        Should.Throw<ArgumentOutOfRangeException>(() => new ConfigurationVersion(value)).ParamName.ShouldBe("value");
    }

    /// <summary>Verifies valid scalar values preserve their exact identity and diagnostic representation.</summary>
    [Fact]
    public void Constructor_WhenScalarValuesAreValid_PreservesValues()
    {
        var id = new SecurityPolicySnapshotId(Guid.Parse("11111111-1111-1111-1111-111111111111"));

        id.ToString().ShouldBe("11111111-1111-1111-1111-111111111111");
        new SecurityProfileVersion(7).ToString().ShouldBe("7");
        new ConfigurationVersion(9).ToString().ShouldBe("9");
        default(SecurityPolicySnapshotId).Value.ShouldBe(Guid.Empty);
        default(SecurityProfileVersion).Value.ShouldBe(0);
        default(ConfigurationVersion).Value.ShouldBe(0);
    }

    /// <summary>Verifies snapshot references reject each default nested value with its owning parameter name.</summary>
    [Fact]
    public void Constructor_WhenSnapshotReferencePartIsDefault_ThrowsExactArgument()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new SecurityPolicySnapshotReference(default, PolicyVersion(), Hash())).ParamName.ShouldBe("id");
        Should.Throw<ArgumentOutOfRangeException>(() => new SecurityPolicySnapshotReference(SnapshotId(), default, Hash())).ParamName.ShouldBe("version");
        Should.Throw<ArgumentNullException>(() => new SecurityPolicySnapshotReference(SnapshotId(), PolicyVersion(), default)).ParamName.ShouldBe("fingerprint");
    }

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

    /// <summary>Verifies a capture request rejects missing trusted facts rather than fabricating defaults.</summary>
    [Fact]
    public void Constructor_WhenCaptureRequestPartIsInvalid_ThrowsExactArgument()
    {
        Should.Throw<ArgumentNullException>(() => new SecurityAuthorizationCaptureRequest(null!, new SecurityProfileKey("default"), new AgentDefinitionRevision(0), new ConfigurationVersion(9), Identity())).ParamName.ShouldBe("scope");
        Should.Throw<ArgumentNullException>(() => new SecurityAuthorizationCaptureRequest(Scope(), default, new AgentDefinitionRevision(0), new ConfigurationVersion(9), Identity())).ParamName.ShouldBe("profileKey");
        Should.Throw<ArgumentOutOfRangeException>(() => new SecurityAuthorizationCaptureRequest(Scope(), new SecurityProfileKey("default"), new AgentDefinitionRevision(0), default, Identity())).ParamName.ShouldBe("configurationVersion");
        Should.Throw<ArgumentNullException>(() => new SecurityAuthorizationCaptureRequest(Scope(), new SecurityProfileKey("default"), new AgentDefinitionRevision(0), new ConfigurationVersion(9), null!)).ParamName.ShouldBe("identity");
    }

    /// <summary>Verifies canonical scope and correlation values reject default nested identities, including record-copy mutation.</summary>
    [Fact]
    public void Constructor_WhenScopeOrCorrelationPartIsDefault_ThrowsExactArgument()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new SecurityAuthorizationScope(default, null, Correlation())).ParamName.ShouldBe("agentId");
        Should.Throw<ArgumentOutOfRangeException>(() => new SecurityAuthorizationScope(AgentId(), default(SessionId), Correlation())).ParamName.ShouldBe("sessionId");
        Should.Throw<ArgumentNullException>(() => new SecurityAuthorizationScope(AgentId(), SessionId(), null!)).ParamName.ShouldBe("correlation");
        Should.Throw<ArgumentOutOfRangeException>(() => new BeforeRunOperationCorrelation(default, null)).ParamName.ShouldBe("operationId");
        Should.Throw<ArgumentOutOfRangeException>(() => new BeforeRunOperationCorrelation(OperationId(), default(AdmissionId))).ParamName.ShouldBe("admissionId");
        Should.Throw<ArgumentOutOfRangeException>(() => new InRunOperationCorrelation(OperationId(), default, null)).ParamName.ShouldBe("runId");
        Should.Throw<ArgumentOutOfRangeException>(() => new InRunOperationCorrelation(OperationId(), RunId(), default(TurnId))).ParamName.ShouldBe("turnId");
        Should.Throw<ArgumentOutOfRangeException>(() => new AfterRunOperationCorrelation(OperationId(), default)).ParamName.ShouldBe("causalRunId");
        Should.Throw<ArgumentNullException>(() => Scope() with { Correlation = null! }).ParamName.ShouldBe("correlation");
    }

    /// <summary>Verifies every hardened init accessor rejects invalid record copies without changing the original.</summary>
    [Fact]
    public void With_WhenScopeOrCorrelationPartIsDefault_ThrowsExactArgumentAndPreservesOriginal()
    {
        var scope = Scope();
        var before = new BeforeRunOperationCorrelation(OperationId(), new AdmissionId(Guid.Parse("66666666-6666-6666-6666-666666666666")));
        var during = new InRunOperationCorrelation(OperationId(), RunId(), new TurnId(Guid.Parse("77777777-7777-7777-7777-777777777777")));
        var after = new AfterRunOperationCorrelation(OperationId(), RunId());

        Should.Throw<ArgumentOutOfRangeException>(() => scope with { AgentId = default }).ParamName.ShouldBe("agentId");
        Should.Throw<ArgumentOutOfRangeException>(() => scope with { SessionId = default(SessionId) }).ParamName.ShouldBe("sessionId");
        Should.Throw<ArgumentNullException>(() => scope with { Correlation = null! }).ParamName.ShouldBe("correlation");
        Should.Throw<ArgumentOutOfRangeException>(() => before with { OperationId = default }).ParamName.ShouldBe("operationId");
        Should.Throw<ArgumentOutOfRangeException>(() => before with { AdmissionId = default(AdmissionId) }).ParamName.ShouldBe("admissionId");
        Should.Throw<ArgumentOutOfRangeException>(() => during with { RunId = default }).ParamName.ShouldBe("runId");
        Should.Throw<ArgumentOutOfRangeException>(() => during with { TurnId = default(TurnId) }).ParamName.ShouldBe("turnId");
        Should.Throw<ArgumentOutOfRangeException>(() => after with { CausalRunId = default }).ParamName.ShouldBe("causalRunId");

        scope.ShouldBe(Scope());
        before.AdmissionId.ShouldNotBeNull();
        during.TurnId.ShouldNotBeNull();
        after.CausalRunId.ShouldBe(RunId());
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
        Capture().ShouldBe(Capture());
        Reference().ShouldBe(Reference());
        typeof(SecurityAuthorizationContext).GetProperties().ShouldAllBe(property => property.SetMethod == null);
        typeof(SecurityAuthorizationCaptureRequest).GetProperties().ShouldAllBe(property => property.SetMethod == null);
        typeof(SecurityPolicySnapshotReference).GetProperties().ShouldAllBe(property => property.SetMethod == null);
    }

    private static SecurityAuthorizationContext Context() => NewContext(new SecurityProfileKey("default"), new SecurityProfileVersion(2), Reference(), new ComponentKey<ISecurityAuthority>("authority"), new ConfigurationVersion(9), Scope(), Identity());

    private static SecurityAuthorizationContext NewContext(SecurityProfileKey profileKey, SecurityProfileVersion profileVersion, SecurityPolicySnapshotReference policySnapshot, ComponentKey<ISecurityAuthority> authorityKey, ConfigurationVersion configurationVersion, SecurityAuthorizationScope scope, ExecutionIdentity identity) => new(profileKey, profileVersion, policySnapshot, authorityKey, new AgentDefinitionRevision(0), configurationVersion, scope, identity);

    private static SecurityAuthorizationCaptureRequest Capture() => new(Scope(), new SecurityProfileKey("default"), new AgentDefinitionRevision(0), new ConfigurationVersion(9), Identity());

    private static SecurityPolicySnapshotReference Reference() => new(SnapshotId(), PolicyVersion(), Hash());
    private static SecurityPolicySnapshotId SnapshotId() => new(Guid.Parse("11111111-1111-1111-1111-111111111111"));
    private static SecurityPolicyVersion PolicyVersion() => new(3);
    private static ContentHash Hash() => new("sha256:test");
    private static AgentId AgentId() => new(Guid.Parse("22222222-2222-2222-2222-222222222222"));
    private static SessionId SessionId() => new(Guid.Parse("33333333-3333-3333-3333-333333333333"));
    private static OperationId OperationId() => new(Guid.Parse("44444444-4444-4444-4444-444444444444"));
    private static RunId RunId() => new(Guid.Parse("55555555-5555-5555-5555-555555555555"));
    private static BeforeRunOperationCorrelation Correlation() => new(OperationId(), null);
    private static SecurityAuthorizationScope Scope() => new(AgentId(), SessionId(), Correlation());
    private static ExecutionIdentity Identity() => TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
}
