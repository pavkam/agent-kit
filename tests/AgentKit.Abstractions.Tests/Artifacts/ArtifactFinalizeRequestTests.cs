// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Artifacts;



/// <summary>Verifies ArtifactFinalizeRequest behavior and contracts.</summary>
public sealed class ArtifactFinalizeRequestTests
{
    [Fact]
    public void ArtifactFinalizeRequest_WhenPreparationIdentityIsEmpty_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ArtifactFinalizeRequest(default, AgentId(), SessionId(), null, Correlation(), Identity(), new IdempotencyKey("finalize")));
        exception.ParamName.ShouldBe("preparationId");
    }

    private static AgentId AgentId() => new(Guid.Parse("30000000-0000-0000-0000-000000000003"));
    private static SessionId SessionId() => new(Guid.Parse("40000000-0000-0000-0000-000000000004"));
    private static InRunOperationCorrelation Correlation() => new(new OperationId(Guid.Parse("60000000-0000-0000-0000-000000000006")), new RunId(Guid.Parse("70000000-0000-0000-0000-000000000007")), null);
    private static ExecutionIdentity Identity() => TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
}
