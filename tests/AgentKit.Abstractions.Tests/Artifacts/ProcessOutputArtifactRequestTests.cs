// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Artifacts;



/// <summary>Verifies ProcessOutputArtifactRequest behavior and contracts.</summary>
public sealed class ProcessOutputArtifactRequestTests
{
    [Fact]
    public void ProcessOutputArtifactRequest_WhenContentIsDefault_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new ProcessOutputArtifactRequest(Intent(), Scope(), Identity(), Authorization(), ProcessOutputKind.StandardOutput, default, new IdempotencyKey("output")));
        exception.ParamName.ShouldBe("content");
    }

    private static ResolvedProcessIntent Intent()
    {
        var request = new ProcessResolveRequest(new ProcessOperationId(Guid.Parse("20000000-0000-0000-0000-000000000002")), "/bin/sh", [], null, [], [], new SandboxProfileId("test"), ProcessWorkspaceAccess.ReadOnly, ProcessSideEffectClass.ReadOnly, ProcessChildPolicy.Deny, new ProcessResourceLimits(TimeSpan.FromSeconds(1), 10, TimeSpan.FromMilliseconds(10)));
        return new ResolvedProcessIntent(request, "/bin/sh", new ContentHash("executable"), "/workspace", "/workspace", new ContentHash("environment"), new ContentHash("input"));
    }

    private static SecurityAuthorizationScope Scope() => new(AgentId(), SessionId(), Correlation());
    private static AgentId AgentId() => new(Guid.Parse("30000000-0000-0000-0000-000000000003"));
    private static SessionId SessionId() => new(Guid.Parse("40000000-0000-0000-0000-000000000004"));
    private static InRunOperationCorrelation Correlation() => new(new OperationId(Guid.Parse("60000000-0000-0000-0000-000000000006")), new RunId(Guid.Parse("70000000-0000-0000-0000-000000000007")), null);
    private static SecurityAuthorizationContext Authorization() => TestSupport.TestSecurityEvidence.Authorization(AgentId(), SessionId(), Correlation(), Identity());
    private static ExecutionIdentity Identity() => TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
}
