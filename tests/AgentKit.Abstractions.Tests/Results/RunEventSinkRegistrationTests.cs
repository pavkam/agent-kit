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

    [Fact]
    public void Constructor_WhenNoFlushDeadlineIsSupplied_UsesTheBoundedDefault()
    {
        var registration = new RunEventSinkRegistration("sink", RunEventDelivery.Required, 0);

        registration.FlushDeadline.ShouldBe(RunEventSinkRegistration.DefaultFlushDeadline);
        RunEventSinkRegistration.DefaultFlushDeadline.ShouldBe(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void Constructor_WhenAFlushDeadlineIsSupplied_RoundTripsIt()
    {
        var registration = new RunEventSinkRegistration("sink", RunEventDelivery.Required, 0, TimeSpan.FromMilliseconds(250));

        registration.FlushDeadline.ShouldBe(TimeSpan.FromMilliseconds(250));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-60_000)]
    public void Constructor_WhenFlushDeadlineIsNegative_ThrowsArgumentOutOfRangeExceptionWithParamName(int milliseconds)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new RunEventSinkRegistration("sink", RunEventDelivery.Required, 0, TimeSpan.FromMilliseconds(milliseconds)));

        exception.ParamName.ShouldBe("flushDeadline");
    }

    [Fact]
    public void Equals_WhenOnlyTheFlushDeadlineDiffers_AreNotEqual() =>
        new RunEventSinkRegistration("sink", RunEventDelivery.Required, 0, TimeSpan.FromSeconds(1))
            .ShouldNotBe(new RunEventSinkRegistration("sink", RunEventDelivery.Required, 0, TimeSpan.FromSeconds(2)));
}
