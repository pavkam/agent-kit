// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

/// <summary>Verifies SessionRunStateResult derived behavior and contracts.</summary>
public sealed class SessionRunStateResultTests
{
    [Fact]
    public void SessionRunStateUnavailable_WhenSafeReasonIsBlank_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new SessionRunStateUnavailable(" ")).ParamName.ShouldBe("safeReason");

    [Fact]
    public void SessionRunStateUnavailable_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SessionRunStateUnavailable("unavailable");
        var copy = original with { };
        copy.ShouldBe(original);
        original.SafeReason.ShouldBe("unavailable");
    }

    [Fact]
    public void SessionRunStateLoaded_With_WhenApplied_ProducesEqualCopy()
    {
        var state = SessionsTestData.AcceptedRunState();
        var original = new SessionRunStateLoaded(state);
        var copy = original with { };
        copy.ShouldBe(original);
        original.State.ShouldBe(state);
        original.AbortRequested.ShouldBeFalse();
    }

    [Fact]
    public void SessionRunStateLoaded_WhenAbortRequested_PreservesTheMarker()
    {
        var state = SessionsTestData.AcceptedRunState();
        var loaded = new SessionRunStateLoaded(state, abortRequested: true);
        loaded.AbortRequested.ShouldBeTrue();
        loaded.State.ShouldBe(state);
    }
}
