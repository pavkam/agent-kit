// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Results;

/// <summary>Verifies RunEventSinkRegistration behavior and contracts.</summary>
public sealed class RunEventSinkRegistrationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_WhenSinkNameIsBlank_ThrowsArgumentExceptionWithParamName(string? sinkName)
    {
        var exception = Should.Throw<ArgumentException>(() => new RunEventSinkRegistration(sinkName!, RunEventDelivery.Required, 0));
        exception.ParamName.ShouldBe("sinkName");
    }

    [Fact]
    public void Constructor_WhenDeliveryIsUndefined_ThrowsArgumentOutOfRangeExceptionWithParamName()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new RunEventSinkRegistration("sink", (RunEventDelivery) (-1), 0));
        exception.ParamName.ShouldBe("delivery");
    }

    [Theory]
    [InlineData(RunEventDelivery.Required)]
    [InlineData(RunEventDelivery.BestEffort)]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties(RunEventDelivery delivery)
    {
        var registration = new RunEventSinkRegistration("sink", delivery, 3);

        registration.SinkName.ShouldBe("sink");
        registration.Delivery.ShouldBe(delivery);
        registration.Order.ShouldBe(3);
    }

    [Fact]
    public void Constructor_WhenOrderIsNegative_IsAccepted()
    {
        var registration = new RunEventSinkRegistration("sink", RunEventDelivery.BestEffort, -1);

        registration.Order.ShouldBe(-1);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new RunEventSinkRegistration("sink", RunEventDelivery.Required, 1);
        var copy = original with { };

        copy.ShouldBe(original);
    }
}
