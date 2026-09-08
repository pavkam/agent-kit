// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

public sealed class SessionCreationAuthorizationGuardTests
{
    [Fact]
    public void ThrowIfInvalidSessionCreationAuthorization_WhenSessionlessBeforeRun_DoesNotThrow()
    {
        var authorization = Authorization(
            new BeforeRunOperationCorrelation(new OperationId(Guid.NewGuid()), null), sessionId: null);

        Should.NotThrow(() => ArgumentException.ThrowIfInvalidSessionCreationAuthorization(authorization));
    }

    [Fact]
    public void ThrowIfInvalidSessionCreationAuthorization_WhenNull_ThrowsExactArgumentNullExceptionWithInferredName()
    {
        SecurityAuthorizationContext authorization = null!;

        var exception = Should.Throw<ArgumentNullException>(() =>
            ArgumentException.ThrowIfInvalidSessionCreationAuthorization(authorization));

        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("authorization");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void ThrowIfInvalidSessionCreationAuthorization_WhenScopeIsNotSessionlessBeforeRun_ThrowsExactArgumentException(
        int invalidCase)
    {
        var authorization = InvalidAuthorization(invalidCase);

        var exception = Should.Throw<ArgumentException>(() =>
            ArgumentException.ThrowIfInvalidSessionCreationAuthorization(authorization));

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("authorization");
    }

    [Fact]
    public void ThrowIfInvalidSessionCreationAuthorization_WhenParamNameIsExplicit_UsesExplicitName()
    {
        var authorization = InvalidAuthorization(0);

        var exception = Should.Throw<ArgumentException>(() =>
            ArgumentException.ThrowIfInvalidSessionCreationAuthorization(authorization, "captured"));

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("captured");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void SessionCreateRequest_WhenAuthorizationIsNotSessionlessBeforeRun_ThrowsExactArgumentExceptionForAuthorization(
        int invalidCase)
    {
        var authorization = InvalidAuthorization(invalidCase);

        var exception = Should.Throw<ArgumentException>(() => Request(authorization));

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("authorization");
    }

    [Fact]
    public void SessionCreateRequest_WhenAuthorizationIsNull_ThrowsExactArgumentNullExceptionForAuthorization()
    {
        var exception = Should.Throw<ArgumentNullException>(() => Request(null!));

        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("authorization");
    }

    private static SessionCreateRequest Request(SecurityAuthorizationContext? authorization)
    {
        var agentId = authorization?.Scope.AgentId ?? new AgentId(Guid.NewGuid());
        var identity = authorization?.Identity ?? TestSupport.TestExecutionIdentity.Create(
            new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
        return new SessionCreateRequest(
            agentId,
            identity,
            authorization!,
            null,
            new IdempotencyKey("create"),
            ExtensionData.Empty);
    }

    private static SecurityAuthorizationContext InvalidAuthorization(int invalidCase) => invalidCase switch
    {
        0 => Authorization(
            new BeforeRunOperationCorrelation(new OperationId(Guid.NewGuid()), null),
            new SessionId(Guid.NewGuid())),
        1 => Authorization(
            new InRunOperationCorrelation(new OperationId(Guid.NewGuid()), new RunId(Guid.NewGuid()), null),
            sessionId: null),
        _ => Authorization(
            new AfterRunOperationCorrelation(new OperationId(Guid.NewGuid()), new RunId(Guid.NewGuid())),
            sessionId: null),
    };

    private static SecurityAuthorizationContext Authorization(
        OperationCorrelation correlation,
        SessionId? sessionId)
    {
        var agentId = new AgentId(Guid.NewGuid());
        var identity = TestSupport.TestExecutionIdentity.Create(
            new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
        return new SecurityAuthorizationContext(
            new SecurityProfileKey("default"),
            new SecurityProfileVersion(1),
            new SecurityPolicySnapshotReference(
                new SecurityPolicySnapshotId(Guid.NewGuid()),
                new SecurityPolicyVersion(1),
                new ContentHash("sha256:policy")),
            new ComponentKey<ISecurityAuthority>("authority"),
            new AgentDefinitionRevision(1),
            new ConfigurationVersion(1),
            new SecurityAuthorizationScope(agentId, sessionId, correlation),
            identity);
    }
}
