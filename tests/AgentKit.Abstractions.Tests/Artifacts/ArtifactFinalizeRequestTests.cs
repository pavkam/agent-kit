// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Artifacts;



/// <summary>Verifies ArtifactFinalizeRequest behavior and contracts.</summary>
public sealed class ArtifactFinalizeRequestTests
{
    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var correlation = Correlation();
        var identity = Identity();
        var key = new IdempotencyKey("finalize");
        var request = new ArtifactFinalizeRequest(PreparationId(), AgentId(), SessionId(), null, correlation, identity, Authorization(), key);
        request.PreparationId.ShouldBe(PreparationId());
        request.AgentId.ShouldBe(AgentId());
        request.SessionId.ShouldBe(SessionId());
        request.ToolCallId.ShouldBeNull();
        request.Correlation.ShouldBe(correlation);
        request.Identity.ShouldBe(identity);
        request.IdempotencyKey.ShouldBe(key);
    }

    [Fact]
    public void ArtifactFinalizeRequest_WhenPreparationIdentityIsEmpty_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ArtifactFinalizeRequest(default, AgentId(), SessionId(), null, Correlation(), Identity(), Authorization(), new IdempotencyKey("finalize")));
        exception.ParamName.ShouldBe("preparationId");
    }

    [Fact]
    public void Constructor_WhenCorrelationIsNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ArtifactFinalizeRequest(PreparationId(), AgentId(), SessionId(), null, null!, Identity(), Authorization(), new IdempotencyKey("finalize")));
        exception.ParamName.ShouldBe("correlation");
    }

    [Fact]
    public void Constructor_WhenIdentityIsNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ArtifactFinalizeRequest(PreparationId(), AgentId(), SessionId(), null, Correlation(), null!, null!, new IdempotencyKey("finalize")));
        exception.ParamName.ShouldBe("identity");
    }

    [Fact]
    public void Constructor_WhenIdempotencyKeyIsBlank_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new ArtifactFinalizeRequest(PreparationId(), AgentId(), SessionId(), null, Correlation(), Identity(), Authorization(), default));
        exception.ParamName.ShouldBe("idempotencyKey");
    }

    private static ArtifactPreparationId PreparationId() => new(Guid.Parse("50000000-0000-0000-0000-000000000005"));
    private static AgentId AgentId() => new(Guid.Parse("30000000-0000-0000-0000-000000000003"));
    private static SessionId SessionId() => new(Guid.Parse("40000000-0000-0000-0000-000000000004"));
    private static InRunOperationCorrelation Correlation() => new(new OperationId(Guid.Parse("60000000-0000-0000-0000-000000000006")), new RunId(Guid.Parse("70000000-0000-0000-0000-000000000007")), null);

    private static SecurityAuthorizationContext Authorization() => TestSupport.TestSecurityEvidence.Authorization(AgentId(), SessionId(), Correlation(), Identity());
    private static ExecutionIdentity Identity() => TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ArtifactFinalizeRequest(PreparationId(), AgentId(), SessionId(), null, Correlation(), Identity(), Authorization(), new IdempotencyKey("finalize"));
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
