// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

/// <summary>Verifies SessionRunStateRequest behavior and contracts.</summary>
public sealed class SessionRunStateRequestTests
{
    [Fact]
    public void Constructor_WhenContextIsNotInRun_ThrowsExactArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new SessionRunStateRequest(SessionsTestData.BeforeRunContext()));
        exception.ParamName.ShouldBe("context");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var context = SessionsTestData.InRunContext();
        var request = new SessionRunStateRequest(context);
        request.Context.ShouldBe(context);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SessionRunStateRequest(SessionsTestData.InRunContext());
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
