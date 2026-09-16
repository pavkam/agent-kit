// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.InMemory.Tests;

/// <summary>Verifies CreationRoute behavior and contracts.</summary>
public sealed class CreationRouteTests
{
    private static readonly SessionCreateRequest _request = TestFactory.CreateRequest();

    private static readonly SessionLocation _location = new(
        new SessionAddress(_request.AgentId, new SessionId(Guid.NewGuid())), _request.Identity.TenantId,
        new SessionStoreKey("store-a"), new SessionDirectoryRevision(1), DateTimeOffset.UnixEpoch, new SchemaVersion("v1"));

    [Fact]
    public void Constructor_WhenRequestIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new CreationRoute(null!, _location));
        exception.ParamName.ShouldBe("request");
    }

    [Fact]
    public void Constructor_WhenLocationIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new CreationRoute(_request, null!));
        exception.ParamName.ShouldBe("location");
    }

    [Fact]
    public void CreationRoute_WhenConstructed_RetainsRequestAndLocation()
    {
        var route = new CreationRoute(_request, _location);

        route.Request.ShouldBe(_request);
        route.Location.ShouldBe(_location);
        var clone = route with { };
        clone.ShouldBe(route);
    }
}
