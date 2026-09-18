// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

/// <summary>Verifies SessionLaneStateRequest behavior and contracts.</summary>
public sealed class SessionLaneStateRequestTests
{
    [Fact]
    public void Constructor_WhenContextIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new SessionLaneStateRequest(null!));
        exception.ParamName.ShouldBe("context");
    }

    [Fact]
    public void Constructor_WhenContextIsNotLaneBound_ThrowsExactArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(
            () => new SessionLaneStateRequest(SessionsTestData.BeforeRunContext(laneBound: false)));
        exception.ParamName.ShouldBe("context");
    }

    [Fact]
    public void Constructor_WhenContextIsNotBeforeRun_ThrowsExactArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new SessionLaneStateRequest(SessionsTestData.InRunContext()));
        exception.ParamName.ShouldBe("context");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var context = SessionsTestData.BeforeRunContext();
        var request = new SessionLaneStateRequest(context);
        request.Context.ShouldBe(context);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SessionLaneStateRequest(SessionsTestData.BeforeRunContext());
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
