// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit;

/// <summary>Verifies ToolExecutionContext behavior and contracts.</summary>
public sealed class ToolExecutionContextTests
{
    [Fact]
    public void ToolExecutionContext_Constructor_WhenCorrelationNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ToolExecutionContext(AgentId(), SessionId(), ToolCallId(), null!, Identity(), null!, null));
        exception.ParamName.ShouldBe("correlation");
    }

    [Fact]
    public void ToolExecutionContext_Constructor_WhenIdentityNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ToolExecutionContext(AgentId(), SessionId(), ToolCallId(), Correlation(), null!, null!, null));
        exception.ParamName.ShouldBe("identity");
    }

    [Fact]
    public void ToolExecutionContext_Constructor_WhenValid_RoundTripsProperties()
    {
        var agentId = AgentId();
        var sessionId = SessionId();
        var callId = ToolCallId();
        var correlation = Correlation();
        var identity = Identity();
        var authorization = TestSupport.TestSecurityEvidence.Authorization(agentId, sessionId, correlation, identity);
        var context = new ToolExecutionContext(agentId, sessionId, callId, correlation, identity, authorization, TestSupport.TestSecurityEvidence.SessionProfile());
        context.AgentId.ShouldBe(agentId);
        context.SessionId.ShouldBe(sessionId);
        context.ToolCallId.ShouldBe(callId);
        context.Correlation.ShouldBe(correlation);
        context.Identity.ShouldBe(identity);
        context.Authorization.ShouldBe(authorization);
        _ = context.SessionProfile.ShouldNotBeNull();
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var agentId = AgentId();
        var sessionId = SessionId();
        var callId = ToolCallId();
        var correlation = Correlation();
        var identity = Identity();
        var authorization = TestSupport.TestSecurityEvidence.Authorization(agentId, sessionId, correlation, identity);
        var original = new ToolExecutionContext(agentId, sessionId, callId, correlation, identity, authorization, TestSupport.TestSecurityEvidence.SessionProfile());
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void ToolExecutionContext_Equality_WhenSameValues_InstancesAreEqual()
    {
        var agentId = AgentId();
        var sessionId = SessionId();
        var callId = ToolCallId();
        var correlation = Correlation();
        var identity = Identity();
        var authorization = TestSupport.TestSecurityEvidence.Authorization(agentId, sessionId, correlation, identity);
        new ToolExecutionContext(agentId, sessionId, callId, correlation, identity, authorization, TestSupport.TestSecurityEvidence.SessionProfile()).ShouldBe(new ToolExecutionContext(agentId, sessionId, callId, correlation, identity, authorization, TestSupport.TestSecurityEvidence.SessionProfile()));
    }

    private static AgentId AgentId() => new(Guid.NewGuid());
    private static SessionId SessionId() => new(Guid.NewGuid());
    private static ToolCallId ToolCallId() => new(Guid.NewGuid());
    private static InRunOperationCorrelation Correlation() => new(new OperationId(Guid.NewGuid()), new RunId(Guid.NewGuid()), null);
    private static ExecutionIdentity Identity() => TestSupport.TestExecutionIdentity.Create(new TenantId("t"), new PrincipalId("p"), ExecutionSubjectKind.Human);
}
