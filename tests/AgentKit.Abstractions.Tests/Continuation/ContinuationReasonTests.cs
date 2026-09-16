// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Continuation;

/// <summary>Verifies ContinuationReason behavior and contracts.</summary>
public sealed class ContinuationReasonTests
{
    [Fact]
    public void Constructor_WhenSelectedCauseIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new ContinuationReason(null!, [])).ParamName.ShouldBe("selectedCause");

    [Fact]
    public void Constructor_WhenOtherPendingCausesContainNull_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new ContinuationReason(Cause(1), [null!])).ParamName.ShouldBe("otherPendingCauses");

    [Fact]
    public void Constructor_WhenOtherPendingCausesContainsSelectedCause_ThrowsExactArgumentException()
    {
        var cause = Cause(1);
        Should.Throw<ArgumentException>(() => new ContinuationReason(cause, [cause])).ParamName.ShouldBe("otherPendingCauses");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var selected = Cause(1);
        var other = Cause(2);
        var reason = new ContinuationReason(selected, [other]);
        reason.SelectedCause.ShouldBe(selected);
        reason.OtherPendingCauses.ShouldBe([other]);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ContinuationReason(Cause(1), [Cause(2)]);
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static ExplicitPolicyContinuationCause Cause(int value) => new($"reason-{value}");
}
