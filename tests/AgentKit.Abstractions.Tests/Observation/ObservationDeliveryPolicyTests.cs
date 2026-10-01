// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Observation;

/// <summary>Verifies <see cref="ObservationDeliveryPolicy"/> validation and defaults.</summary>
public sealed class ObservationDeliveryPolicyTests
{
    [Fact]
    public void Constructor_WhenNoArgumentsAreSupplied_IsBestEffortWithBoundedDefaultDeadline()
    {
        var policy = new ObservationDeliveryPolicy();

        policy.Required.ShouldBeFalse();
        policy.FlushDeadline.ShouldBe(TimeSpan.FromSeconds(30));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-5000)]
    public void Constructor_WhenDeadlineIsNegative_ThrowsArgumentOutOfRangeExceptionNamingDeadline(int milliseconds)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new ObservationDeliveryPolicy(true, TimeSpan.FromMilliseconds(milliseconds)));

        exception.ParamName.ShouldBe("flushDeadline");
    }

    [Fact]
    public void Constructor_WhenRequiredWithExplicitDeadline_PreservesBoth()
    {
        var policy = new ObservationDeliveryPolicy(true, TimeSpan.FromSeconds(2));

        policy.Required.ShouldBeTrue();
        policy.FlushDeadline.ShouldBe(TimeSpan.FromSeconds(2));
    }
}
