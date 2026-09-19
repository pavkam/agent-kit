// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

/// <summary>Verifies SessionPendingInputsRequest behavior and contracts.</summary>
public sealed class SessionPendingInputsRequestTests
{
    [Fact]
    public void Constructor_WhenContextIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new SessionPendingInputsRequest(null!));
        exception.ParamName.ShouldBe("context");
    }

    [Fact]
    public void Constructor_WhenContextIsNotLaneBound_ThrowsExactArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(
            () => new SessionPendingInputsRequest(SessionsTestData.BeforeRunContext(laneBound: false)));
        exception.ParamName.ShouldBe("context");
    }

    [Fact]
    public void Constructor_WhenContextIsBeforeRun_RoundTripsProperties()
    {
        var context = SessionsTestData.BeforeRunContext();
        var request = new SessionPendingInputsRequest(context);
        request.Context.ShouldBe(context);
    }

    [Fact]
    public void Constructor_WhenContextIsInRun_RoundTripsProperties()
    {
        var context = SessionsTestData.InRunContext();
        var request = new SessionPendingInputsRequest(context);
        request.Context.ShouldBe(context);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SessionPendingInputsRequest(SessionsTestData.BeforeRunContext());
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
