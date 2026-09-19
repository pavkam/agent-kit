// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Security;

/// <summary>Verifies GrantRevocationResult derived behavior and contracts.</summary>
public sealed class GrantRevocationResultTests
{
    private static readonly GrantId GrantId = new(Guid.NewGuid());

    [Fact]
    public void GrantRevoked_WhenGrantIdIsDefault_ThrowsExactArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new GrantRevoked(default, new RevocationReason(SecurityRevocationTrigger.Explicit, "message")));

    [Fact]
    public void GrantRevoked_WhenReasonIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new GrantRevoked(GrantId, null!)).ParamName.ShouldBe("reason");

    [Fact]
    public void GrantRevoked_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var reason = new RevocationReason(SecurityRevocationTrigger.Explicit, "message");
        var result = new GrantRevoked(GrantId, reason);
        result.GrantId.ShouldBe(GrantId);
        result.Reason.ShouldBe(reason);
    }

    [Fact]
    public void GrantRevoked_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new GrantRevoked(GrantId, new RevocationReason(SecurityRevocationTrigger.Explicit, "message"));
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void GrantAlreadyRevoked_WhenGrantIdIsDefault_ThrowsExactArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new GrantAlreadyRevoked(default));

    [Fact]
    public void GrantAlreadyRevoked_WhenArgumentsAreValid_RoundTripsProperties() =>
        new GrantAlreadyRevoked(GrantId).GrantId.ShouldBe(GrantId);

    [Fact]
    public void GrantAlreadyRevoked_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new GrantAlreadyRevoked(GrantId);
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void GrantRevocationNotFound_WhenGrantIdIsDefault_ThrowsExactArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new GrantRevocationNotFound(default));

    [Fact]
    public void GrantRevocationNotFound_WhenArgumentsAreValid_RoundTripsProperties() =>
        new GrantRevocationNotFound(GrantId).GrantId.ShouldBe(GrantId);

    [Fact]
    public void GrantRevocationNotFound_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new GrantRevocationNotFound(GrantId);
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void GrantRevocationUnavailable_WhenSafeReasonIsBlank_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new GrantRevocationUnavailable(GrantId, " ")).ParamName.ShouldBe("safeReason");

    [Fact]
    public void GrantRevocationUnavailable_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var result = new GrantRevocationUnavailable(GrantId, "unavailable");
        result.GrantId.ShouldBe(GrantId);
        result.SafeReason.ShouldBe("unavailable");
    }

    [Fact]
    public void GrantRevocationUnavailable_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new GrantRevocationUnavailable(GrantId, "unavailable");
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
