// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

using AgentKit;

/// <summary>Verifies SessionEntryCodecLimits behavior and contracts.</summary>
public sealed class SessionEntryCodecLimitsTests
{
    [Theory]
    [InlineData(0, 1, 1, 1, "maximumPayloadBytes")]
    [InlineData(-1, 1, 1, 1, "maximumPayloadBytes")]
    [InlineData(1, 0, 1, 1, "maximumExtensionCount")]
    [InlineData(1, -1, 1, 1, "maximumExtensionCount")]
    [InlineData(1, 1, 0, 1, "maximumExtensionBytes")]
    [InlineData(1, 1, -1, 1, "maximumExtensionBytes")]
    [InlineData(1, 1, 1, 0, "maximumJsonDepth")]
    [InlineData(1, 1, 1, -1, "maximumJsonDepth")]
    public void SessionEntryCodecLimits_Constructor_WhenLimitIsNotPositive_ThrowsArgumentOutOfRangeException(int maximumPayloadBytes, int maximumExtensionCount, int maximumExtensionBytes, int maximumJsonDepth, string parameterName)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new SessionEntryCodecLimits(maximumPayloadBytes, maximumExtensionCount, maximumExtensionBytes, maximumJsonDepth));
        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe(parameterName);
    }

    [Fact]
    public void SessionEntryCodecLimits_Constructor_WhenLimitsArePositive_PreservesEveryFiniteBound()
    {
        var limits = new SessionEntryCodecLimits(int.MaxValue, 1, int.MaxValue, 64);
        limits.MaximumPayloadBytes.ShouldBe(int.MaxValue);
        limits.MaximumExtensionCount.ShouldBe(1);
        limits.MaximumExtensionBytes.ShouldBe(int.MaxValue);
        limits.MaximumJsonDepth.ShouldBe(64);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SessionEntryCodecLimits(1024, 8, 512, 16);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
