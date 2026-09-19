// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Security;

/// <summary>Verifies ApprovalResolutionResult derived behavior and contracts.</summary>
public sealed class ApprovalResolutionResultTests
{
    [Fact]
    public void ApprovalResolved_WhenResponseIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new ApprovalResolved(null!)).ParamName.ShouldBe("response");

    [Fact]
    public void ApprovalResolved_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var response = SecurityAbstractionsTestData.Response();
        new ApprovalResolved(response).Response.ShouldBe(response);
    }

    [Fact]
    public void ApprovalResolved_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ApprovalResolved(SecurityAbstractionsTestData.Response());
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void ApprovalAlreadyResolved_WhenResponseIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new ApprovalAlreadyResolved(null!)).ParamName.ShouldBe("response");

    [Fact]
    public void ApprovalAlreadyResolved_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var response = SecurityAbstractionsTestData.Response();
        new ApprovalAlreadyResolved(response).Response.ShouldBe(response);
    }

    [Fact]
    public void ApprovalAlreadyResolved_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ApprovalAlreadyResolved(SecurityAbstractionsTestData.Response());
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void ApprovalResolutionExpired_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ApprovalResolutionExpired();
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void ApprovalResolutionConflict_WhenSafeReasonIsBlank_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new ApprovalResolutionConflict(" ")).ParamName.ShouldBe("safeReason");

    [Fact]
    public void ApprovalResolutionConflict_WhenArgumentsAreValid_RoundTripsProperties() =>
        new ApprovalResolutionConflict("conflict").SafeReason.ShouldBe("conflict");

    [Fact]
    public void ApprovalResolutionConflict_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ApprovalResolutionConflict("conflict");
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void ApprovalResolutionUnavailable_WhenSafeReasonIsBlank_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new ApprovalResolutionUnavailable(" ")).ParamName.ShouldBe("safeReason");

    [Fact]
    public void ApprovalResolutionUnavailable_WhenArgumentsAreValid_RoundTripsProperties() =>
        new ApprovalResolutionUnavailable("unavailable").SafeReason.ShouldBe("unavailable");

    [Fact]
    public void ApprovalResolutionUnavailable_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ApprovalResolutionUnavailable("unavailable");
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
