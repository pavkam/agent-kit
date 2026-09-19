// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies <see cref="ProviderRetryPolicy"/>.</summary>
public sealed class ProviderRetryPolicyTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WhenMaximumAttemptsIsNotPositive_ThrowsArgumentOutOfRangeException(int attempts)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() =>
            new ProviderRetryPolicy(attempts, TimeSpan.Zero, TimeSpan.Zero));
        exception.ParamName.ShouldBe("maximumAttempts");
    }

    [Fact]
    public void Constructor_WhenInitialDelayIsNegative_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() =>
            new ProviderRetryPolicy(1, TimeSpan.FromMilliseconds(-1), TimeSpan.Zero));
        exception.ParamName.ShouldBe("initialDelay");
    }

    [Fact]
    public void Constructor_WhenMaximumDelayIsBelowInitialDelay_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() =>
            new ProviderRetryPolicy(2, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1)));
        exception.ParamName.ShouldBe("maximumDelay");
    }

    [Fact]
    public void Constructor_WhenBoundsAreValid_PreservesThem()
    {
        var policy = new ProviderRetryPolicy(3, TimeSpan.Zero, TimeSpan.FromSeconds(1));
        policy.MaximumAttempts.ShouldBe(3);
        policy.InitialDelay.ShouldBe(TimeSpan.Zero);
        policy.MaximumDelay.ShouldBe(TimeSpan.FromSeconds(1));
        policy.ShouldBe(new ProviderRetryPolicy(3, TimeSpan.Zero, TimeSpan.FromSeconds(1)));
    }
}
