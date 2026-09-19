// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Security;

/// <summary>Verifies ApprovalValidityWindow behavior and contracts.</summary>
public sealed class ApprovalValidityWindowTests
{
    [Fact]
    public void Constructor_WhenExpiresAtIsNotLaterThanNotBefore_ThrowsExactArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ApprovalValidityWindow(DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch)).ParamName.ShouldBe("expiresAt");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var window = new ApprovalValidityWindow(DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(1));
        window.NotBefore.ShouldBe(DateTimeOffset.UnixEpoch);
        window.ExpiresAt.ShouldBe(DateTimeOffset.UnixEpoch.AddMinutes(1));
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ApprovalValidityWindow(DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(1));
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
