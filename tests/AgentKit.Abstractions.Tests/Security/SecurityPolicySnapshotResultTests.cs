// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Security;

/// <summary>Verifies SecurityPolicySnapshotResult derived behavior and contracts.</summary>
public sealed class SecurityPolicySnapshotResultTests
{
    [Fact]
    public void SecurityPolicySnapshotResolved_WhenReferenceIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new SecurityPolicySnapshotResolved(null!)).ParamName.ShouldBe("reference");

    [Fact]
    public void SecurityPolicySnapshotResolved_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var reference = SecurityAbstractionsTestData.PolicySnapshotReference();
        var result = new SecurityPolicySnapshotResolved(reference);
        result.Reference.ShouldBe(reference);
    }

    [Fact]
    public void SecurityPolicySnapshotResolved_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SecurityPolicySnapshotResolved(SecurityAbstractionsTestData.PolicySnapshotReference());
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void SecurityPolicySnapshotStale_WhenReferenceIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new SecurityPolicySnapshotStale(null!, "reason")).ParamName.ShouldBe("reference");

    [Fact]
    public void SecurityPolicySnapshotStale_WhenSafeReasonIsBlank_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new SecurityPolicySnapshotStale(SecurityAbstractionsTestData.PolicySnapshotReference(), " ")).ParamName.ShouldBe("safeReason");

    [Fact]
    public void SecurityPolicySnapshotStale_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var reference = SecurityAbstractionsTestData.PolicySnapshotReference();
        var result = new SecurityPolicySnapshotStale(reference, "reason");
        result.Reference.ShouldBe(reference);
        result.SafeReason.ShouldBe("reason");
    }

    [Fact]
    public void SecurityPolicySnapshotStale_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SecurityPolicySnapshotStale(SecurityAbstractionsTestData.PolicySnapshotReference(), "reason");
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void SecurityPolicySnapshotUnavailable_WhenSafeReasonIsBlank_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new SecurityPolicySnapshotUnavailable(" ")).ParamName.ShouldBe("safeReason");

    [Fact]
    public void SecurityPolicySnapshotUnavailable_WhenArgumentsAreValid_RoundTripsProperties() =>
        new SecurityPolicySnapshotUnavailable("reason").SafeReason.ShouldBe("reason");

    [Fact]
    public void SecurityPolicySnapshotUnavailable_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SecurityPolicySnapshotUnavailable("reason");
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
