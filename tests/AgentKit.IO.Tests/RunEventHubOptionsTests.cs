// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO.Tests;

public sealed class RunEventHubOptionsTests
{
    [Theory]
    [InlineData(0, 1, "maximumSubscriptions")]
    [InlineData(-1, 1, "maximumSubscriptions")]
    [InlineData(1, 0, "capacityPerSubscription")]
    [InlineData(1, -1, "capacityPerSubscription")]
    public void Constructor_WhenBoundsAreInvalid_ThrowsExactArgumentOutOfRange(int subscribers, int capacity, string parameter)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new RunEventHubOptions(subscribers, capacity));
        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe(parameter);
    }

    [Fact]
    public void Constructor_WhenBoundsAreOmitted_SelectsDocumentedPackageDefaults()
    {
        var options = new RunEventHubOptions();

        options.MaximumSubscriptions.ShouldBe(32);
        options.CapacityPerSubscription.ShouldBe(256);
    }

    [Fact]
    public void Constructor_WhenBoundsAreSupplied_RetainsThemExactly()
    {
        var options = new RunEventHubOptions(3, 7);

        options.MaximumSubscriptions.ShouldBe(3);
        options.CapacityPerSubscription.ShouldBe(7);
    }

    [Fact]
    public void With_WhenNoMembersChanged_ProducesAnEqualClone()
    {
        var options = new RunEventHubOptions(3, 7);
        var clone = options with { };
        clone.ShouldBe(options);
        clone.ShouldNotBeSameAs(options);
    }
}
