// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Context;

/// <summary>Verifies <see cref="ContextAssemblyEvidence"/> boundary guards.</summary>
public sealed class ContextAssemblyEvidenceTests
{
    [Fact]
    public void Constructor_WhenAgentIsNull_ThrowsBeforeReadingOtherEvidence() =>
        Should.Throw<ArgumentNullException>(() => new ContextAssemblyEvidence(null!, null!, null!, null!, null!)).ParamName.ShouldBe("agent");

    [Fact]
    public void Constructor_WhenCapturedCoordinatesAndRevisionsAgree_PreservesAtomicEvidence()
    {
        var evidence = Create();
        evidence.History.SourceCursor.AgentId.ShouldBe(evidence.Agent.Id);
        evidence.Authorization.Identity.ShouldBe(evidence.Identity);
        evidence.Authorization.ConfigurationVersion.ShouldBe(evidence.Configuration.Version);
    }

    [Fact]
    public void Constructor_WhenAuthorizationAgentDiffers_Throws()
    {
        var valid = Create();
        var captured = valid.Authorization;
        var scope = captured.Scope with { AgentId = new AgentId(Guid.Parse("10000000-0000-0000-0000-000000000099")) };
        var authorization = new SecurityAuthorizationContext(captured.ProfileKey, captured.ProfileVersion, captured.PolicySnapshot, captured.AuthorityKey, captured.AgentDefinitionRevision, captured.ConfigurationVersion, scope, captured.Identity);

        Should.Throw<ArgumentException>(() => new ContextAssemblyEvidence(valid.Agent, valid.Identity, valid.History, authorization, valid.Configuration)).ParamName.ShouldBe("authorization");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = Create();
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static ContextAssemblyEvidence Create()
    {
        var agentId = new AgentId(Guid.Parse("10000000-0000-0000-0000-000000000001"));
        var sessionId = new SessionId(Guid.Parse("20000000-0000-0000-0000-000000000001"));
        var branchId = new BranchId(Guid.Parse("30000000-0000-0000-0000-000000000001"));
        var revision = new AgentDefinitionRevision(1);
        var agent = new AgentDefinition(agentId, revision, "agent", new ModelSelectionPolicy([new ModelAlias("chat")]), ModelRequirements.None, [], [], LlmToolChoice.Auto, LlmRequestSettings.Default, new RunPolicyDefaults(8, TimeSpan.FromMinutes(1)), ExtensionData.Empty, new SecurityProfileKey("security"), new SessionProfileKey("session"));
        var identity = TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
        var correlation = new InRunOperationCorrelation(new OperationId(Guid.Parse("40000000-0000-0000-0000-000000000001")), new RunId(Guid.Parse("50000000-0000-0000-0000-000000000001")), new TurnId(Guid.Parse("60000000-0000-0000-0000-000000000001")));
        var scope = new SecurityAuthorizationScope(agentId, sessionId, correlation);
        var authorization = new SecurityAuthorizationContext(new SecurityProfileKey("security"), new SecurityProfileVersion(1), new SecurityPolicySnapshotReference(new SecurityPolicySnapshotId(Guid.Parse("70000000-0000-0000-0000-000000000001")), new SecurityPolicyVersion(1), new ContentHash("sha256:policy")), new ComponentKey<ISecurityAuthority>("authority"), revision, new ConfigurationVersion(1), scope, identity);
        var history = new HistoryView(new MessageCursor(agentId, sessionId, null, branchId, new SessionVersion(1), new SessionSequence(0)), [], []);
        var configuration = new EffectiveConfigurationSnapshot(new ConfigurationVersion(1), new ContentHash("sha256:configuration"), [], []);
        return new ContextAssemblyEvidence(agent, identity, history, authorization, configuration);
    }
}
