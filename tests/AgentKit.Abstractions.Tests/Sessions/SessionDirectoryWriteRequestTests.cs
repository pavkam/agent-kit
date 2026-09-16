// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

/// <summary>Verifies SessionDirectoryWriteRequest behavior and contracts.</summary>
public sealed class SessionDirectoryWriteRequestTests
{
    [Fact]
    public void Constructor_WhenContextIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new SessionDirectoryWriteRequest(null!, SessionsTestData.Location(), new IdempotencyKey("write")));
        exception.ParamName.ShouldBe("context");
    }

    [Fact]
    public void Constructor_WhenLocationIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new SessionDirectoryWriteRequest(SessionsTestData.BeforeRunContext(), null!, new IdempotencyKey("write")));
        exception.ParamName.ShouldBe("location");
    }

    [Fact]
    public void Constructor_WhenLocationAddressDiffersFromContext_ThrowsExactArgumentException()
    {
        var location = SessionsTestData.Location(address: new SessionAddress(new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid())));
        var exception = Should.Throw<ArgumentException>(() => new SessionDirectoryWriteRequest(SessionsTestData.BeforeRunContext(), location, new IdempotencyKey("write")));
        exception.ParamName.ShouldBe("location");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var context = SessionsTestData.BeforeRunContext();
        var location = SessionsTestData.Location();
        var request = new SessionDirectoryWriteRequest(context, location, new IdempotencyKey("write"));
        request.Context.ShouldBe(context);
        request.Location.ShouldBe(location);
        request.IdempotencyKey.ShouldBe(new IdempotencyKey("write"));
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SessionDirectoryWriteRequest(SessionsTestData.BeforeRunContext(), SessionsTestData.Location(), new IdempotencyKey("write"));
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
