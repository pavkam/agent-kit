// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO.Tests;

/// <summary>Verifies DefaultOutputBackpressurePolicy behavior and contracts.</summary>
public sealed class DefaultOutputBackpressurePolicyTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WhenMaximumBestEffortWaitIsNotPositive_ThrowsArgumentOutOfRangeException(int seconds) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new DefaultOutputBackpressurePolicy(TimeSpan.FromSeconds(seconds)));

    [Fact]
    public void Constructor_WhenArgumentIsValid_RoundTripsMaximumBestEffortWait()
    {
        var policy = new DefaultOutputBackpressurePolicy(TimeSpan.FromSeconds(3));

        policy.MaximumBestEffortWait.ShouldBe(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task DecideAsync_WhenDeliveryIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        var policy = new DefaultOutputBackpressurePolicy(TimeSpan.FromSeconds(1));

        _ = await Should.ThrowAsync<ArgumentOutOfRangeException>(
            () => policy.DecideAsync((RunEventDelivery) (-1), TimeSpan.Zero, TestContext.Current.CancellationToken).AsTask());
    }

    [Fact]
    public async Task DecideAsync_WhenBlockedForIsNegative_ThrowsArgumentOutOfRangeException()
    {
        var policy = new DefaultOutputBackpressurePolicy(TimeSpan.FromSeconds(1));

        _ = await Should.ThrowAsync<ArgumentOutOfRangeException>(
            () => policy.DecideAsync(RunEventDelivery.BestEffort, TimeSpan.FromSeconds(-1), TestContext.Current.CancellationToken).AsTask());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1000)]
    [InlineData(100_000)]
    public async Task DecideAsync_WhenDeliveryIsRequired_AlwaysReturnsWait(int blockedSeconds)
    {
        var policy = new DefaultOutputBackpressurePolicy(TimeSpan.FromSeconds(1));

        var decision = await policy.DecideAsync(
            RunEventDelivery.Required, TimeSpan.FromSeconds(blockedSeconds), TestContext.Current.CancellationToken);

        decision.ShouldBe(BackpressureDecision.Wait);
    }

    [Fact]
    public async Task DecideAsync_WhenBestEffortIsBlockedWithinTheGracePeriod_ReturnsWait()
    {
        var policy = new DefaultOutputBackpressurePolicy(TimeSpan.FromSeconds(5));

        var decision = await policy.DecideAsync(
            RunEventDelivery.BestEffort, TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        decision.ShouldBe(BackpressureDecision.Wait);
    }

    [Fact]
    public async Task DecideAsync_WhenBestEffortExceedsTheGracePeriod_ReturnsDrop()
    {
        var policy = new DefaultOutputBackpressurePolicy(TimeSpan.FromSeconds(5));

        var decision = await policy.DecideAsync(
            RunEventDelivery.BestEffort, TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        decision.ShouldBe(BackpressureDecision.Drop);
    }
}
