// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Artifacts;



/// <summary>Verifies ArtifactAbortRequest behavior and contracts.</summary>
public sealed class ArtifactAbortRequestTests
{
    [Fact]
    public void ArtifactAbortRequest_WhenReasonIsUndefined_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ArtifactAbortRequest(PreparationId(), AgentId(), SessionId(), Correlation(), Identity(), (ArtifactAbortReason) 999, new IdempotencyKey("abort")));
        exception.ParamName.ShouldBe("reason");
    }

    private static AgentId AgentId() => new(Guid.Parse("30000000-0000-0000-0000-000000000003"));
    private static SessionId SessionId() => new(Guid.Parse("40000000-0000-0000-0000-000000000004"));
    private static ArtifactPreparationId PreparationId() => new(Guid.Parse("50000000-0000-0000-0000-000000000005"));
    private static InRunOperationCorrelation Correlation() => new(new OperationId(Guid.Parse("60000000-0000-0000-0000-000000000006")), new RunId(Guid.Parse("70000000-0000-0000-0000-000000000007")), null);
    private static ExecutionIdentity Identity() => TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
}
