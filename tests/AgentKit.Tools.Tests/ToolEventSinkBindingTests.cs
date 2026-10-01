// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Tests;

/// <summary>Verifies <see cref="ToolEventSinkBinding"/> validation.</summary>
public sealed class ToolEventSinkBindingTests
{
    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var registration = new ToolEventSinkRegistration(new ComponentId("a"), 0);
        var sink = new NullSink();

        var binding = new ToolEventSinkBinding(registration, sink);

        binding.Registration.ShouldBeSameAs(registration);
        binding.Sink.ShouldBeSameAs(sink);
    }

    [Fact]
    public void Constructor_WhenArgumentIsNull_ThrowsExactParameter()
    {
        Should.Throw<ArgumentNullException>(() => new ToolEventSinkBinding(null!, new NullSink())).ParamName.ShouldBe("registration");
        Should.Throw<ArgumentNullException>(() => new ToolEventSinkBinding(new ToolEventSinkRegistration(new ComponentId("a"), 0), null!)).ParamName.ShouldBe("sink");
    }

    private sealed class NullSink: IToolEventSink
    {
        public ValueTask PublishAsync(ToolEvent toolEvent, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }
}
