// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Security;

/// <summary>Verifies ApprovalHandlerResult derived behavior and contracts.</summary>
public sealed class ApprovalHandlerResultTests
{
    [Fact]
    public void ApprovalHandlerResponded_WhenResponseIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new ApprovalHandlerResponded(null!)).ParamName.ShouldBe("Response");

    [Fact]
    public void ApprovalHandlerResponded_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var response = SecurityAbstractionsTestData.Response();
        var result = new ApprovalHandlerResponded(response);
        result.Response.ShouldBe(response);
    }

    [Fact]
    public void ApprovalHandlerResponded_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ApprovalHandlerResponded(SecurityAbstractionsTestData.Response());
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void ApprovalHandlerUnavailable_WhenSafeReasonIsBlank_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new ApprovalHandlerUnavailable(" ")).ParamName.ShouldBe("SafeReason");

    [Fact]
    public void ApprovalHandlerUnavailable_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var result = new ApprovalHandlerUnavailable("unavailable");
        result.SafeReason.ShouldBe("unavailable");
    }

    [Fact]
    public void ApprovalHandlerUnavailable_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ApprovalHandlerUnavailable("unavailable");
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
