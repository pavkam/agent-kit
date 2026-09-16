// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.InMemory.Tests;

/// <summary>Verifies DirectoryWriteRoute behavior and contracts.</summary>
public sealed class DirectoryWriteRouteTests
{
    private static readonly SessionOperationContext _context = TestFactory.OperationContext(
        new SessionAddress(new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid())));

    private static readonly SessionLocation _location = new(
        _context.ToAddress(), _context.Identity.TenantId, new SessionStoreKey("store-a"),
        new SessionDirectoryRevision(1), DateTimeOffset.UnixEpoch, new SchemaVersion("v1"));

    [Fact]
    public void Constructor_WhenRequestIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new DirectoryWriteRoute(null!, _location));
        exception.ParamName.ShouldBe("request");
    }

    [Fact]
    public void Constructor_WhenLocationIsNull_ThrowsArgumentNullException()
    {
        var request = new SessionDirectoryWriteRequest(_context, _location, new IdempotencyKey("route"));
        var exception = Should.Throw<ArgumentNullException>(() => new DirectoryWriteRoute(request, null!));
        exception.ParamName.ShouldBe("location");
    }

    [Fact]
    public void DirectoryWriteRoute_WhenConstructed_RetainsRequestAndLocation()
    {
        var request = new SessionDirectoryWriteRequest(_context, _location, new IdempotencyKey("route"));

        var route = new DirectoryWriteRoute(request, _location);

        route.Request.ShouldBe(request);
        route.Location.ShouldBe(_location);
        var clone = route with { };
        clone.ShouldBe(route);
    }
}
