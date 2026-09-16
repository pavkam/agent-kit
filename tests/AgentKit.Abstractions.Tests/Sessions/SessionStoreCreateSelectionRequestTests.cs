// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

/// <summary>Verifies SessionStoreCreateSelectionRequest behavior and contracts.</summary>
public sealed class SessionStoreCreateSelectionRequestTests
{
    [Fact]
    public void Constructor_WhenRequestIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new SessionStoreCreateSelectionRequest(null!, SessionsTestData.ProfileSnapshot()));
        exception.ParamName.ShouldBe("request");
    }

    [Fact]
    public void Constructor_WhenProfileIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new SessionStoreCreateSelectionRequest(SessionsTestData.CreateRequest(), null!));
        exception.ParamName.ShouldBe("profile");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var request = SessionsTestData.CreateRequest();
        var profile = SessionsTestData.ProfileSnapshot();
        var selection = new SessionStoreCreateSelectionRequest(request, profile);
        selection.Request.ShouldBe(request);
        selection.Profile.ShouldBe(profile);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SessionStoreCreateSelectionRequest(SessionsTestData.CreateRequest(), SessionsTestData.ProfileSnapshot());
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
