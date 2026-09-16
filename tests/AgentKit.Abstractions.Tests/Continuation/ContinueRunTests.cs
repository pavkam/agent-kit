// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Continuation;

/// <summary>Verifies ContinueRun behavior and contracts.</summary>
public sealed class ContinueRunTests
{
    [Fact]
    public void Constructor_WhenReasonIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new ContinueRun(null!)).ParamName.ShouldBe("reason");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var reason = Reason();
        var decision = new ContinueRun(reason);
        decision.Reason.ShouldBe(reason);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ContinueRun(Reason());
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static ContinuationReason Reason() => new(new ExplicitPolicyContinuationCause("code"), []);
}
