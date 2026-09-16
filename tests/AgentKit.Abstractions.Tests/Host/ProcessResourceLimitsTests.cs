// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;



/// <summary>Verifies ProcessResourceLimits behavior and contracts.</summary>
public sealed class ProcessResourceLimitsTests
{
    [Fact]
    public void ProcessResourceLimits_WhenTimeoutIsNotPositive_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ProcessResourceLimits(TimeSpan.Zero, 100, TimeSpan.FromSeconds(1)));
        exception.ParamName.ShouldBe("timeout");
    }

    [Fact]
    public void ProcessResourceLimits_WhenMaximumOutputBytesIsNotPositive_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ProcessResourceLimits(TimeSpan.FromSeconds(1), 0, TimeSpan.FromSeconds(1))).ParamName.ShouldBe("maximumOutputBytes");

    [Fact]
    public void ProcessResourceLimits_WhenTerminationGracePeriodIsNegative_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ProcessResourceLimits(TimeSpan.FromSeconds(1), 100, TimeSpan.FromSeconds(-1))).ParamName.ShouldBe("terminationGracePeriod");

    [Fact]
    public void ProcessResourceLimits_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var limits = new ProcessResourceLimits(TimeSpan.FromSeconds(10), 1024, TimeSpan.FromSeconds(1));
        limits.Timeout.ShouldBe(TimeSpan.FromSeconds(10));
        limits.MaximumOutputBytes.ShouldBe(1024);
        limits.TerminationGracePeriod.ShouldBe(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void ProcessResourceLimits_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ProcessResourceLimits(TimeSpan.FromSeconds(10), 1024, TimeSpan.FromSeconds(1));
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
