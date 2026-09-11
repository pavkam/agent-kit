// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit.TestSupport;

public sealed class ToolDiscoveryRequestTests
{
    private static readonly AgentId _agent = new(Guid.Parse("10000000-0000-0000-0000-000000000001"));
    private static readonly SessionId _session = new(Guid.Parse("20000000-0000-0000-0000-000000000002"));
    private static readonly RunId _run = new(Guid.Parse("30000000-0000-0000-0000-000000000003"));
    private static readonly OperationId _operation = new(Guid.Parse("40000000-0000-0000-0000-000000000004"));

    [Fact]
    public void Constructor_WhenEvidenceAgrees_RetainsFullContextAndSelectionOrder()
    {
        var identity = Identity();
        var authorization = Authorization();
        var configuration = Configuration();
        var capabilities = Capabilities();
        ImmutableArray<ToolsetReference> toolsets = [Toolset("write"), Toolset("read")];
        var request = new ToolDiscoveryRequest(_agent, _session, _run, identity, authorization,
            new AgentDefinitionRevision(1), configuration, toolsets, capabilities);
        request.AgentId.ShouldBe(_agent);
        request.SessionId.ShouldBe(_session);
        request.RunId.ShouldBe(_run);
        request.Identity.ShouldBeSameAs(identity);
        request.Authorization.ShouldBeSameAs(authorization);
        request.Configuration.ShouldBeSameAs(configuration);
        request.ModelCapabilities.ShouldBeSameAs(capabilities);
        request.AgentDefinitionRevision.ShouldBe(new AgentDefinitionRevision(1));
        request.Toolsets.ShouldBe(toolsets);
    }

    [Fact]
    public void Constructor_WhenRevisionIsZeroAndSelectionEmpty_AcceptsValidBoundaries()
    {
        var authorization = Authorization(revision: new AgentDefinitionRevision(0));
        var request = new ToolDiscoveryRequest(_agent, _session, _run, Identity(), authorization,
            default, Configuration(), [], Capabilities());
        request.AgentDefinitionRevision.Value.ShouldBe(0);
        request.Toolsets.IsDefault.ShouldBeFalse();
        request.Toolsets.ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_WhenIdentitiesAreDefault_RejectsExactParameters()
    {
        AssertExact<ArgumentOutOfRangeException>(() => Create(agent: default(AgentId)), "agentId");
        AssertExact<ArgumentOutOfRangeException>(() => Create(session: default(SessionId)), "sessionId");
        AssertExact<ArgumentOutOfRangeException>(() => Create(run: default(RunId)), "runId");
    }

    [Fact]
    public void Constructor_WhenReferencesAreNull_RejectsExactParameters()
    {
        AssertExact<ArgumentNullException>(() => _ = new ToolDiscoveryRequest(_agent, _session, _run, null!, Authorization(), new(1), Configuration(), [], Capabilities()), "identity");
        AssertExact<ArgumentNullException>(() => _ = new ToolDiscoveryRequest(_agent, _session, _run, Identity(), null!, new(1), Configuration(), [], Capabilities()), "authorization");
        AssertExact<ArgumentNullException>(() => _ = new ToolDiscoveryRequest(_agent, _session, _run, Identity(), Authorization(), new(1), null!, [], Capabilities()), "configuration");
        AssertExact<ArgumentNullException>(() => _ = new ToolDiscoveryRequest(_agent, _session, _run, Identity(), Authorization(), new(1), Configuration(), [], null!), "modelCapabilities");
    }

    [Fact]
    public void Constructor_WhenAuthorizationScopeDiffers_RejectsAgentSessionAndRunSubstitution()
    {
        var differentAgent = new AgentId(Guid.Parse("10000000-0000-0000-0000-000000000009"));
        var differentSession = new SessionId(Guid.Parse("20000000-0000-0000-0000-000000000009"));
        var differentRun = new RunId(Guid.Parse("30000000-0000-0000-0000-000000000009"));
        SecurityAuthorizationScope[] scopes =
        [
            new(differentAgent, _session, Correlation()),
            new(_agent, differentSession, Correlation()),
            new(_agent, null, Correlation()),
            new(_agent, _session, new InRunOperationCorrelation(_operation, differentRun, null)),
            new(_agent, _session, new BeforeRunOperationCorrelation(_operation, null)),
            new(_agent, _session, new AfterRunOperationCorrelation(_operation, _run)),
        ];
        foreach (var scope in scopes)
        {
            AssertExact<ArgumentException>(() => Create(authorization: Authorization(scope: scope)), "authorization");
        }
    }

    [Fact]
    public void Constructor_WhenCompleteIdentityDiffers_RejectsTenantPrincipalAndNormalizationSubstitution()
    {
        var identity = Identity();
        ExecutionIdentity[] different =
        [
            TestExecutionIdentity.Create(new TenantId("other"), identity.PrincipalId, identity.SubjectKind),
            TestExecutionIdentity.Create(identity.TenantId, new PrincipalId("other"), identity.SubjectKind),
            new(identity.TenantId, identity.PrincipalId, identity.SubjectKind, identity.Evidence, [], [], identity.Assurance, new IdentityVersion(2)),
        ];
        foreach (var candidate in different)
        {
            AssertExact<ArgumentException>(() => Create(authorization: Authorization(identity: candidate)), "authorization");
        }
    }

    [Fact]
    public void Constructor_WhenPublicationVersionsDiffer_RejectsDefinitionAndConfigurationSubstitution()
    {
        AssertExact<ArgumentException>(() => Create(authorization: Authorization(revision: new(2))), "authorization");
        AssertExact<ArgumentException>(() => Create(authorization: Authorization(configurationVersion: new(2))), "authorization");
    }

    [Fact]
    public void Constructor_WhenSelectionIsMalformed_RejectsBeforeAnySourceCanBeSelected()
    {
        AssertExact<ArgumentException>(() => Create(toolsets: default(ImmutableArray<ToolsetReference>)), "toolsets");
        AssertExact<ArgumentException>(() => Create(toolsets: [null!]), "toolsets");
        AssertExact<ArgumentException>(() => Create(toolsets: [Toolset("read"), Toolset("read")]), "toolsets");
        AssertExact<ArgumentException>(() => Create(toolsets: [Toolset("read"), new(new ToolsetKey("read"), new ToolExecutionPolicyKey("other"))]), "toolsets");
    }

    [Fact]
    public void Constructor_WhenSelectionKeysDifferByCase_PreservesOrdinalKeys()
    {
        var request = Create(toolsets: [Toolset("read"), Toolset("Read")]);
        request.Toolsets.Select(static toolset => toolset.Key.Value).ShouldBe(["read", "Read"]);
    }

    [Fact]
    public void Equals_WhenContextIsReconstructed_UsesCompleteStructuralEvidence()
    {
        var first = Create(toolsets: [Toolset("read"), Toolset("write")]);
        var second = Create(toolsets: [Toolset("read"), Toolset("write")]);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
        (first with { }).ShouldBe(second);
        new HashSet<ToolDiscoveryRequest> { first, second }.Count.ShouldBe(1);
        first.Equals(null).ShouldBeFalse();
    }

    [Fact]
    public void Equals_WhenSelectionOrSemanticConfigurationDiffers_DistinguishesRequests()
    {
        var original = Create(toolsets: [Toolset("read"), Toolset("write")]);
        original.ShouldNotBe(Create(toolsets: [Toolset("write"), Toolset("read")]));
        original.ShouldNotBe(Create(toolsets: [Toolset("read")]));
        original.ShouldNotBe(Create(configuration: Configuration("sha256:different"), toolsets: original.Toolsets));
        original.ShouldNotBe(Create(capabilities: Capabilities() with { SupportsStreaming = false }, toolsets: original.Toolsets));
        var operation = new OperationId(Guid.Parse("40000000-0000-0000-0000-000000000009"));
        original.ShouldNotBe(Create(authorization: Authorization(scope: new(_agent, _session, new InRunOperationCorrelation(operation, _run, null))), toolsets: original.Toolsets));
    }

    private static ToolDiscoveryRequest Create(AgentId? agent = null, SessionId? session = null, RunId? run = null,
        SecurityAuthorizationContext? authorization = null, EffectiveConfigurationSnapshot? configuration = null,
        ImmutableArray<ToolsetReference>? toolsets = null, ModelCapabilities? capabilities = null) =>
        new(agent ?? _agent, session ?? _session, run ?? _run, Identity(), authorization ?? Authorization(), new(1),
            configuration ?? Configuration(), toolsets ?? [], capabilities ?? Capabilities());

    private static SecurityAuthorizationContext Authorization(SecurityAuthorizationScope? scope = null, ExecutionIdentity? identity = null,
        AgentDefinitionRevision? revision = null, ConfigurationVersion? configurationVersion = null)
    {
        var evidence = TestSecurityEvidence.Authorization(_agent, _session, Correlation(), Identity());
        return new SecurityAuthorizationContext(evidence.ProfileKey, evidence.ProfileVersion, evidence.PolicySnapshot,
            evidence.AuthorityKey, revision ?? evidence.AgentDefinitionRevision, configurationVersion ?? evidence.ConfigurationVersion,
            scope ?? evidence.Scope, identity ?? evidence.Identity);
    }
    private static ExecutionIdentity Identity() => TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
    private static InRunOperationCorrelation Correlation() => new(_operation, _run, null);
    private static EffectiveConfigurationSnapshot Configuration(string fingerprint = "sha256:configuration") => new(new(1), new ContentHash(fingerprint), [], []);
    private static ToolsetReference Toolset(string key) => new(new ToolsetKey(key), new ToolExecutionPolicyKey("policy"));
    private static ModelCapabilities Capabilities() => new(true, true, true, true, false, false, false, ExtensionData.Empty);
    private static void AssertExact<TException>(Action action, string parameter) where TException : ArgumentException
    {
        var exception = Should.Throw<TException>(action);
        exception.GetType().ShouldBe(typeof(TException));
        exception.ParamName.ShouldBe(parameter);
    }
}
