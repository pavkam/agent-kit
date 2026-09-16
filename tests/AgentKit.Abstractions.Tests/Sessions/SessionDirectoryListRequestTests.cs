// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

/// <summary>Verifies SessionDirectoryListRequest behavior and contracts.</summary>
public sealed class SessionDirectoryListRequestTests
{
    [Fact]
    public void Constructor_WhenAgentIdIsDefault_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => Request(agentId: default(AgentId)));
        exception.ParamName.ShouldBe("agentId");
    }

    [Fact]
    public void Constructor_WhenIdentityIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => Request(omitIdentity: true));
        exception.ParamName.ShouldBe("identity");
    }

    [Fact]
    public void Constructor_WhenAuthorizationIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => Request(omitAuthorization: true));
        exception.ParamName.ShouldBe("authorization");
    }

    [Fact]
    public void Constructor_WhenAuthorizationIdentityDiffers_ThrowsExactArgumentException()
    {
        var other = TestSupport.TestExecutionIdentity.Create(new TenantId("other"), new PrincipalId("other"), ExecutionSubjectKind.Human);
        var exception = Should.Throw<ArgumentException>(() => Request(authorization: SessionsTestData.Authorization(SessionsTestData.BeforeRun(), null, identity: other)));
        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("authorization");
    }

    [Fact]
    public void Constructor_WhenAuthorizationAgentDiffers_ThrowsExactArgumentException()
    {
        var authorization = new SecurityAuthorizationContext(
            new SecurityProfileKey("security"), new SecurityProfileVersion(1),
            new SecurityPolicySnapshotReference(new SecurityPolicySnapshotId(Guid.Parse("a0000000-0000-0000-0000-00000000000a")), new SecurityPolicyVersion(1), new ContentHash("sha256:policy")),
            new ComponentKey<ISecurityAuthority>("authority"), new AgentDefinitionRevision(1), new ConfigurationVersion(1),
            new SecurityAuthorizationScope(new AgentId(Guid.NewGuid()), null, SessionsTestData.BeforeRun()), SessionsTestData.Identity());
        var exception = Should.Throw<ArgumentException>(() => Request(authorization: authorization));
        exception.ParamName.ShouldBe("authorization");
    }

    [Fact]
    public void Constructor_WhenAuthorizationScopesASession_ThrowsExactArgumentException()
    {
        var authorization = SessionsTestData.Authorization(SessionsTestData.BeforeRun(), SessionsTestData.SessionId);
        var exception = Should.Throw<ArgumentException>(() => Request(authorization: authorization));
        exception.ParamName.ShouldBe("authorization");
    }

    [Fact]
    public void Constructor_WhenMaximumResultsIsNotPositive_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => Request(maximumResults: 0));
        exception.ParamName.ShouldBe("maximumResults");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var identity = SessionsTestData.Identity();
        var authorization = SessionsTestData.Authorization(SessionsTestData.BeforeRun(), null, identity: identity);
        var request = Request(identity: identity, authorization: authorization);
        request.AgentId.ShouldBe(SessionsTestData.AgentId);
        request.Identity.ShouldBe(identity);
        request.Authorization.ShouldBe(authorization);
        request.AfterSessionId.ShouldBeNull();
        request.MaximumResults.ShouldBe(10);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = Request();
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static SessionDirectoryListRequest Request(AgentId? agentId = null, ExecutionIdentity? identity = null,
        SecurityAuthorizationContext? authorization = null, bool omitIdentity = false, bool omitAuthorization = false,
        int maximumResults = 10)
    {
        var resolvedIdentity = identity ?? SessionsTestData.Identity();
        var resolvedAuthorization = authorization ?? SessionsTestData.Authorization(SessionsTestData.BeforeRun(), null, identity: resolvedIdentity);
        return new SessionDirectoryListRequest(agentId ?? SessionsTestData.AgentId,
            omitIdentity ? null! : resolvedIdentity, omitAuthorization ? null! : resolvedAuthorization, null, maximumResults);
    }
}
