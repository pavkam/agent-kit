// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Security;

/// <summary>Verifies ApprovalResponderAuthorizationResult derived behavior and contracts.</summary>
public sealed class ApprovalResponderAuthorizationResultTests
{
    [Fact]
    public void ApprovalResponderAuthorized_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ApprovalResponderAuthorized();
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void ApprovalResponderUnauthorized_WhenSafeReasonIsBlank_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new ApprovalResponderUnauthorized(" ")).ParamName.ShouldBe("SafeReason");

    [Fact]
    public void ApprovalResponderUnauthorized_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var result = new ApprovalResponderUnauthorized("unauthorized");
        result.SafeReason.ShouldBe("unauthorized");
    }

    [Fact]
    public void ApprovalResponderUnauthorized_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ApprovalResponderUnauthorized("unauthorized");
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
