// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit;

/// <summary>Verifies <see cref="ToolRetryPolicy"/> validation and deterministic backoff.</summary>
public sealed class ToolRetryPolicyTests
{
    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var policy = new ToolRetryPolicy(4, TimeSpan.FromMilliseconds(100), 2.0, TimeSpan.FromSeconds(1), 0.25);

        policy.MaximumAttempts.ShouldBe(4);
        policy.InitialDelay.ShouldBe(TimeSpan.FromMilliseconds(100));
        policy.BackoffMultiplier.ShouldBe(2.0);
        policy.MaximumDelay.ShouldBe(TimeSpan.FromSeconds(1));
        policy.JitterFraction.ShouldBe(0.25);
    }

    [Theory]
    [InlineData(0, 0, 1.0, 0, 0.0, "maximumAttempts")]
    [InlineData(-1, 0, 1.0, 0, 0.0, "maximumAttempts")]
    [InlineData(1, -1, 1.0, 1000, 0.0, "initialDelay")]
    [InlineData(1, 100, 1.0, 50, 0.0, "maximumDelay")]
    [InlineData(1, 0, 0.99, 0, 0.0, "backoffMultiplier")]
    [InlineData(1, 0, double.NaN, 0, 0.0, "backoffMultiplier")]
    [InlineData(1, 0, double.PositiveInfinity, 0, 0.0, "backoffMultiplier")]
    [InlineData(1, 0, 1.0, 0, -0.01, "jitterFraction")]
    [InlineData(1, 0, 1.0, 0, 1.01, "jitterFraction")]
    [InlineData(1, 0, 1.0, 0, double.NaN, "jitterFraction")]
    public void Constructor_WhenArgumentIsOutOfRange_ThrowsExactParameter(
        int attempts, int initialMilliseconds, double multiplier, int maximumMilliseconds, double jitter, string parameter)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ToolRetryPolicy(
            attempts,
            TimeSpan.FromMilliseconds(initialMilliseconds),
            multiplier,
            TimeSpan.FromMilliseconds(maximumMilliseconds),
            jitter));

        exception.ParamName.ShouldBe(parameter);
    }

    [Fact]
    public void NoRetry_WhenRead_PerformsExactlyOneAttempt()
    {
        ToolRetryPolicy.NoRetry.MaximumAttempts.ShouldBe(1);
        ToolRetryPolicy.NoRetry.ComputeDelay(1, 0.0).ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void ComputeDelay_WhenNoJitter_GrowsGeometricallyUpToTheMaximum()
    {
        var policy = new ToolRetryPolicy(10, TimeSpan.FromMilliseconds(100), 2.0, TimeSpan.FromMilliseconds(500), 0.0);

        policy.ComputeDelay(1, 0.0).ShouldBe(TimeSpan.FromMilliseconds(100));
        policy.ComputeDelay(2, 0.0).ShouldBe(TimeSpan.FromMilliseconds(200));
        policy.ComputeDelay(3, 0.0).ShouldBe(TimeSpan.FromMilliseconds(400));
        policy.ComputeDelay(4, 0.0).ShouldBe(TimeSpan.FromMilliseconds(500));
        policy.ComputeDelay(9, 0.0).ShouldBe(TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    public void ComputeDelay_WhenJitterApplies_RemovesAtMostTheConfiguredFractionDeterministically()
    {
        var policy = new ToolRetryPolicy(3, TimeSpan.FromMilliseconds(1000), 1.0, TimeSpan.FromMilliseconds(1000), 0.5);

        policy.ComputeDelay(1, 0.0).ShouldBe(TimeSpan.FromMilliseconds(1000));
        policy.ComputeDelay(1, 0.5).ShouldBe(TimeSpan.FromMilliseconds(750));
        policy.ComputeDelay(1, 1.0).ShouldBe(TimeSpan.FromMilliseconds(500));
        policy.ComputeDelay(1, 0.5).ShouldBe(policy.ComputeDelay(1, 0.5));
    }

    [Fact]
    public void ComputeDelay_WhenFailedAttemptIsNotPositive_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => ToolRetryPolicy.NoRetry.ComputeDelay(0, 0.0));

        exception.ParamName.ShouldBe("failedAttempt");
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(double.NaN)]
    public void ComputeDelay_WhenRandomIsOutsideTheUnitInterval_ThrowsExactParameter(double unitRandom)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => ToolRetryPolicy.NoRetry.ComputeDelay(1, unitRandom));

        exception.ParamName.ShouldBe("unitRandom");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ToolRetryPolicy(2, TimeSpan.Zero, 1.0, TimeSpan.Zero, 0.0);

        (original with { }).ShouldBe(original);
    }
}
