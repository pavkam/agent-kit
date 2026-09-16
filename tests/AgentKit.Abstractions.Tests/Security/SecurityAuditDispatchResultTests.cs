// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Security;

/// <summary>Verifies SecurityAuditDispatchResult derived behavior and contracts.</summary>
public sealed class SecurityAuditDispatchResultTests
{
    [Fact]
    public void SecurityAuditAccepted_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SecurityAuditAccepted();
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void SecurityAuditFailed_WhenSafeReasonIsBlank_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new SecurityAuditFailed(" ")).ParamName.ShouldBe("safeReason");

    [Fact]
    public void SecurityAuditFailed_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var result = new SecurityAuditFailed("failed");
        result.SafeReason.ShouldBe("failed");
    }

    [Fact]
    public void SecurityAuditFailed_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SecurityAuditFailed("failed");
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void SecurityAuditTimedOut_WhenSafeReasonIsBlank_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new SecurityAuditTimedOut(" ")).ParamName.ShouldBe("safeReason");

    [Fact]
    public void SecurityAuditTimedOut_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var result = new SecurityAuditTimedOut("timed out");
        result.SafeReason.ShouldBe("timed out");
    }

    [Fact]
    public void SecurityAuditTimedOut_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SecurityAuditTimedOut("timed out");
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void SecurityAuditUnavailable_WhenSafeReasonIsBlank_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new SecurityAuditUnavailable(" ")).ParamName.ShouldBe("safeReason");

    [Fact]
    public void SecurityAuditUnavailable_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var result = new SecurityAuditUnavailable("unavailable");
        result.SafeReason.ShouldBe("unavailable");
    }

    [Fact]
    public void SecurityAuditUnavailable_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SecurityAuditUnavailable("unavailable");
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
