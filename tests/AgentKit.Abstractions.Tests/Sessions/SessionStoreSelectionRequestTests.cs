// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

/// <summary>Verifies SessionStoreSelectionRequest behavior and contracts.</summary>
public sealed class SessionStoreSelectionRequestTests
{
    [Fact]
    public void Constructor_WhenContextIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new SessionStoreSelectionRequest(null!, SessionsTestData.ProfileSnapshot(), SessionsTestData.Location()));
        exception.ParamName.ShouldBe("context");
    }

    [Fact]
    public void Constructor_WhenProfileIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new SessionStoreSelectionRequest(SessionsTestData.BeforeRunContext(), null!, SessionsTestData.Location()));
        exception.ParamName.ShouldBe("profile");
    }

    [Fact]
    public void Constructor_WhenLocationIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new SessionStoreSelectionRequest(SessionsTestData.BeforeRunContext(), SessionsTestData.ProfileSnapshot(), null!));
        exception.ParamName.ShouldBe("location");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var context = SessionsTestData.BeforeRunContext();
        var profile = SessionsTestData.ProfileSnapshot();
        var location = SessionsTestData.Location();
        var request = new SessionStoreSelectionRequest(context, profile, location);
        request.Context.ShouldBe(context);
        request.Profile.ShouldBe(profile);
        request.Location.ShouldBe(location);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SessionStoreSelectionRequest(SessionsTestData.BeforeRunContext(), SessionsTestData.ProfileSnapshot(), SessionsTestData.Location());
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
