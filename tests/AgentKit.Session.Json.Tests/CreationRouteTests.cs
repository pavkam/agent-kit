// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Json.Tests;

/// <summary>Verifies the replay evidence retained for one successful directory creation route.</summary>
public sealed class CreationRouteTests
{
    /// <summary>Verifies both captured values are retained unchanged.</summary>
    [Fact]
    public void Constructor_WhenValuesAreValid_RetainsRequestAndLocation()
    {
        var request = CreateRequest();
        var location = Location(request);

        var route = new CreationRoute(request, location);

        route.Request.ShouldBeSameAs(request);
        route.Location.ShouldBeSameAs(location);
    }

    /// <summary>Verifies a null creation request is rejected.</summary>
    [Fact]
    public void Constructor_WhenRequestIsNull_ThrowsExactParameter()
    {
        var request = CreateRequest();
        Should.Throw<ArgumentNullException>(() => new CreationRoute(null!, Location(request)))
            .ParamName.ShouldBe("request");
    }

    /// <summary>Verifies a null location is rejected.</summary>
    [Fact]
    public void Constructor_WhenLocationIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new CreationRoute(CreateRequest(), null!))
            .ParamName.ShouldBe("location");

    /// <summary>Verifies two routes built from identical evidence compare equal.</summary>
    [Fact]
    public void Equals_WhenRequestAndLocationMatch_IsEqual()
    {
        var request = CreateRequest();
        var location = Location(request);

        new CreationRoute(request, location).ShouldBe(new CreationRoute(request, location));
    }

    private static SessionCreateRequest CreateRequest()
    {
        var agentId = JsonSessionStoreTests.Identifier<AgentId>(200);
        var identity = TestExecutionIdentity.Create(
            new TenantId("tenant-route"), new PrincipalId("owner"), ExecutionSubjectKind.Human);
        var correlation = new BeforeRunOperationCorrelation(JsonSessionStoreTests.Identifier<OperationId>(201), null);
        var authorization = new SecurityAuthorizationContext(
            new SecurityProfileKey("route"), new SecurityProfileVersion(1),
            new SecurityPolicySnapshotReference(JsonSessionStoreTests.Identifier<SecurityPolicySnapshotId>(202),
                new SecurityPolicyVersion(1), new ContentHash("sha256:route-policy")),
            new ComponentKey<ISecurityAuthority>("route"), new AgentDefinitionRevision(1),
            new ConfigurationVersion(1), new SecurityAuthorizationScope(agentId, null, correlation), identity);
        return new SessionCreateRequest(
            agentId, identity, authorization, null, new IdempotencyKey("route-create"), ExtensionData.Empty);
    }

    private static SessionLocation Location(SessionCreateRequest request) => new(
        new SessionAddress(request.AgentId, JsonSessionStoreTests.Identifier<SessionId>(203)), request.Identity.TenantId,
        new SessionStoreKey("agentkit.json"), new SessionDirectoryRevision(1), DateTimeOffset.UnixEpoch,
        new SchemaVersion("1"));
}
