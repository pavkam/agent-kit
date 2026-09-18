// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations.Tests;

/// <summary>Verifies <see cref="ConversationOutputEvent"/> argument checks and value semantics.</summary>
public sealed class ConversationOutputEventTests
{
    [Fact]
    public void Constructor_WhenOutputIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new ConversationOutputEvent(null!)).ParamName.ShouldBe("output");

    [Fact]
    public void Constructor_WhenOutputIsSupplied_ExposesIt()
    {
        var output = new ValidatedOutput(OutputMode.Text, "hello", json: null, value: null);

        var outputEvent = new ConversationOutputEvent(output);

        outputEvent.Output.ShouldBeSameAs(output);
        _ = outputEvent.ShouldBeAssignableTo<ConversationEvent>();
    }

    [Fact]
    public void Equals_WhenOutputsAreEqual_ReturnsTrue()
    {
        var output = new ValidatedOutput(OutputMode.Text, "hello", json: null, value: null);

        new ConversationOutputEvent(output).ShouldBe(new ConversationOutputEvent(output));
    }
}
