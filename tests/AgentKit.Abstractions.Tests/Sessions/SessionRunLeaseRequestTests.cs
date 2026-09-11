// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

using AgentKit.TestSupport;

/// <summary>Verifies SessionRunLeaseRequest behavior and contracts.</summary>
public sealed class SessionRunLeaseRequestTests
{
    [Fact]
    public void SessionRunLeaseRequest_WhenArgumentsAreInvalid_ThrowsExactExceptionAndParamName()
    {
        Should.Throw<ArgumentNullException>(() => new SessionRunLeaseRequest(null!, new OperationStateRevision(1))).ParamName.ShouldBe("context");
        Should.Throw<ArgumentException>(() => new SessionRunLeaseRequest(Context(false, true), new OperationStateRevision(1))).ParamName.ShouldBe("context");
        Should.Throw<ArgumentOutOfRangeException>(() => new SessionRunLeaseRequest(Context(true, true), default)).ParamName.ShouldBe("expectedStateRevision");
    }

    private static SessionOperationContext Context(bool inRun, bool laneBound)
    {
        var agentId = new AgentId(Guid.NewGuid());
        var sessionId = new SessionId(Guid.NewGuid());
        OperationCorrelation correlation = inRun ? new InRunOperationCorrelation(new OperationId(Guid.NewGuid()), new RunId(Guid.NewGuid()), null) : new BeforeRunOperationCorrelation(new OperationId(Guid.NewGuid()), null);
        var identity = TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
        return new SessionOperationContext(agentId, sessionId, laneBound ? new ExecutionLaneId(Guid.NewGuid()) : null, correlation, identity, TestSecurityEvidence.Authorization(agentId, sessionId, correlation, identity));
    }
}
