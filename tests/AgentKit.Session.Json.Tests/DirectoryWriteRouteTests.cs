// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Json.Tests;

/// <summary>Verifies the replay evidence retained for one committed directory route write.</summary>
public sealed class DirectoryWriteRouteTests
{
    /// <summary>Verifies both captured values are retained unchanged.</summary>
    [Fact]
    public void Constructor_WhenValuesAreValid_RetainsRequestAndLocation()
    {
        var write = WriteRequest();

        var route = new DirectoryWriteRoute(write, write.Location);

        route.Request.ShouldBeSameAs(write);
        route.Location.ShouldBeSameAs(write.Location);
    }

    /// <summary>Verifies a null write request is rejected.</summary>
    [Fact]
    public void Constructor_WhenRequestIsNull_ThrowsExactParameter()
    {
        var write = WriteRequest();
        Should.Throw<ArgumentNullException>(() => new DirectoryWriteRoute(null!, write.Location))
            .ParamName.ShouldBe("request");
    }

    /// <summary>Verifies a null location is rejected.</summary>
    [Fact]
    public void Constructor_WhenLocationIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new DirectoryWriteRoute(WriteRequest(), null!))
            .ParamName.ShouldBe("location");

    /// <summary>Verifies two routes built from identical evidence compare equal.</summary>
    [Fact]
    public void Equals_WhenRequestAndLocationMatch_IsEqual()
    {
        var write = WriteRequest();

        new DirectoryWriteRoute(write, write.Location).ShouldBe(new DirectoryWriteRoute(write, write.Location));
    }

    private static SessionDirectoryWriteRequest WriteRequest()
    {
        var agentId = JsonSessionStoreTests.Identifier<AgentId>(210);
        var sessionId = JsonSessionStoreTests.Identifier<SessionId>(211);
        var identity = TestExecutionIdentity.Create(
            new TenantId("tenant-write"), new PrincipalId("owner"), ExecutionSubjectKind.Human);
        var correlation = new BeforeRunOperationCorrelation(JsonSessionStoreTests.Identifier<OperationId>(212), null);
        var authorization = new SecurityAuthorizationContext(
            new SecurityProfileKey("write"), new SecurityProfileVersion(1),
            new SecurityPolicySnapshotReference(JsonSessionStoreTests.Identifier<SecurityPolicySnapshotId>(213),
                new SecurityPolicyVersion(1), new ContentHash("sha256:write-policy")),
            new ComponentKey<ISecurityAuthority>("write"), new AgentDefinitionRevision(1),
            new ConfigurationVersion(1), new SecurityAuthorizationScope(agentId, sessionId, correlation), identity);
        var context = new SessionOperationContext(
            agentId, sessionId, null, correlation, identity, authorization);
        var location = new SessionLocation(
            context.ToAddress(), identity.TenantId, new SessionStoreKey("agentkit.json"),
            new SessionDirectoryRevision(1), DateTimeOffset.UnixEpoch, new SchemaVersion("1"));
        return new SessionDirectoryWriteRequest(context, location, new IdempotencyKey("route-write"));
    }
}
