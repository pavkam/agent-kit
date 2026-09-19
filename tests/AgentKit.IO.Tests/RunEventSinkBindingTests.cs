// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO.Tests;

using AgentKit.TestSupport;

/// <summary>Verifies RunEventSinkBinding behavior and contracts.</summary>
public sealed class RunEventSinkBindingTests
{
    [Fact]
    public void Constructor_WhenRegistrationIsNull_ThrowsArgumentNullExceptionWithParamName() =>
        Should.Throw<ArgumentNullException>(() => new RunEventSinkBinding(null!, new FakeRunEventSink())).ParamName.ShouldBe("registration");

    [Fact]
    public void Constructor_WhenSinkIsNull_ThrowsArgumentNullExceptionWithParamName() =>
        Should.Throw<ArgumentNullException>(
            () => new RunEventSinkBinding(new RunEventSinkRegistration("sink", RunEventDelivery.Required, 0), null!))
            .ParamName.ShouldBe("sink");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsRegistration()
    {
        var registration = new RunEventSinkRegistration("sink", RunEventDelivery.BestEffort, 2);
        var binding = new RunEventSinkBinding(registration, new FakeRunEventSink());

        binding.Registration.ShouldBeSameAs(registration);
    }

    [Fact]
    public async Task PublishAsync_ForwardsToTheWrappedSink()
    {
        var inner = new FakeRunEventSink();
        var binding = new RunEventSinkBinding(new RunEventSinkRegistration("sink", RunEventDelivery.Required, 0), inner);
        var runEvent = RunResultTestData.Event(1);

        await binding.PublishAsync(runEvent, TestContext.Current.CancellationToken);

        _ = inner.Received.ShouldHaveSingleItem();
        inner.Received[0].ShouldBeSameAs(runEvent);
    }
}
