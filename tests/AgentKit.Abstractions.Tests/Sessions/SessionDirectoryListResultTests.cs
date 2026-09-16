// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

/// <summary>Verifies SessionDirectoryListResult behavior and contracts.</summary>
public sealed class SessionDirectoryListResultTests
{
    [Fact]
    public void SessionDirectoryListUnavailable_WhenSafeMessageIsBlank_ThrowsExactArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new SessionDirectoryListUnavailable(" "));
        exception.ParamName.ShouldBe("safeMessage");
    }

    [Fact]
    public void SessionDirectoryListUnavailable_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var result = new SessionDirectoryListUnavailable("unavailable");
        result.SafeMessage.ShouldBe("unavailable");
    }

    [Fact]
    public void SessionDirectoryListUnavailable_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SessionDirectoryListUnavailable("unavailable");
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void SessionDirectoryPage_WhenLocationsIsDefault_ThrowsExactArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new SessionDirectoryPage(default, null));
        exception.ParamName.ShouldBe("locations");
    }

    [Fact]
    public void SessionDirectoryPage_WhenArgumentsAreValid_RoundTripsProperties()
    {
        ImmutableArray<SessionLocation> locations = [SessionsTestData.Location()];
        var page = new SessionDirectoryPage(locations, SessionsTestData.SessionId);
        page.Locations.ShouldBe(locations);
        page.NextCursor.ShouldBe(SessionsTestData.SessionId);
    }

    [Fact]
    public void SessionDirectoryPage_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SessionDirectoryPage([SessionsTestData.Location()], null);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
