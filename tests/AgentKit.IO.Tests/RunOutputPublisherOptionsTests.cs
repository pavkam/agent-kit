// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO.Tests;

public sealed class RunOutputPublisherOptionsTests
{
    [Fact]
    public void Constructor_WhenBoundsAreOmitted_SelectsDocumentedPackageDefaults()
    {
        var options = new RunOutputPublisherOptions();

        options.MaximumSubscriptions.ShouldBe(32);
        options.CapacityPerSubscription.ShouldBe(256);
    }

    [Theory]
    [InlineData(0, 1, "maximumSubscriptions")]
    [InlineData(-1, 1, "maximumSubscriptions")]
    [InlineData(1, 0, "capacityPerSubscription")]
    [InlineData(1, -1, "capacityPerSubscription")]
    public void Constructor_WhenABoundIsNotPositive_RejectsExactArgument(int subscriptions, int capacity, string parameter) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new RunOutputPublisherOptions(subscriptions, capacity))
            .ParamName.ShouldBe(parameter);

    [Fact]
    public void Constructor_WhenBoundsAreSupplied_RetainsThemExactly()
    {
        var options = new RunOutputPublisherOptions(3, 7);

        options.MaximumSubscriptions.ShouldBe(3);
        options.CapacityPerSubscription.ShouldBe(7);
    }

    [Fact]
    public void ToHubOptions_WhenProjected_PreservesEachBoundWithoutWidening()
    {
        var hubOptions = new RunOutputPublisherOptions(4, 9).ToHubOptions();

        hubOptions.MaximumSubscriptions.ShouldBe(4);
        hubOptions.CapacityPerSubscription.ShouldBe(9);
    }

    [Fact]
    public void Equals_WhenBoundsMatch_ComparesStructurally()
    {
        new RunOutputPublisherOptions(5, 11).ShouldBe(new RunOutputPublisherOptions(5, 11));
        new RunOutputPublisherOptions(5, 11).ShouldNotBe(new RunOutputPublisherOptions(5, 12));
    }
}
