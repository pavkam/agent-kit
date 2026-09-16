// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

/// <summary>Verifies SessionDirectoryCreateRecordRequest behavior and contracts.</summary>
public sealed class SessionDirectoryCreateRecordRequestTests
{
    [Fact]
    public void Constructor_WhenRequestIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new SessionDirectoryCreateRecordRequest(null!, SessionsTestData.Location()));
        exception.ParamName.ShouldBe("request");
    }

    [Fact]
    public void Constructor_WhenLocationIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new SessionDirectoryCreateRecordRequest(SessionsTestData.CreateRequest(), null!));
        exception.ParamName.ShouldBe("location");
    }

    [Fact]
    public void Constructor_WhenLocationAgentDiffersFromRequest_ThrowsExactArgumentException()
    {
        var location = SessionsTestData.Location(address: new SessionAddress(new AgentId(Guid.NewGuid()), SessionsTestData.SessionId));
        var exception = Should.Throw<ArgumentException>(() => new SessionDirectoryCreateRecordRequest(SessionsTestData.CreateRequest(), location));
        exception.ParamName.ShouldBe("location");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var request = SessionsTestData.CreateRequest();
        var location = SessionsTestData.Location();
        var recordRequest = new SessionDirectoryCreateRecordRequest(request, location);
        recordRequest.Request.ShouldBe(request);
        recordRequest.Location.ShouldBe(location);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SessionDirectoryCreateRecordRequest(SessionsTestData.CreateRequest(), SessionsTestData.Location());
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
