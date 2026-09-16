// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Tests.Http;

using AgentKit.Providers.Http;

/// <summary>Verifies the argument constraints and value semantics of <see cref="ServerSentEvent"/>.</summary>
public sealed class ServerSentEventTests
{
    [Fact]
    public void Constructor_WhenDataIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ServerSentEvent("message", null!, null, null));

        exception.ParamName.ShouldBe("data");
    }

    [Fact]
    public void Constructor_WhenRetryIsNegative_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ServerSentEvent(null, "payload", null, -1));

        exception.ParamName.ShouldBe("retry");
    }

    [Fact]
    public void Constructor_WhenRetryIsZero_IsAccepted()
    {
        var streamEvent = new ServerSentEvent(null, "payload", null, 0);

        streamEvent.Retry.ShouldBe(0);
    }

    [Fact]
    public void Constructor_WhenAllValuesSupplied_ExposesThemUnchanged()
    {
        var streamEvent = new ServerSentEvent("content_block_delta", "{\"a\":1}\n{\"b\":2}", "evt-7", 3000);

        streamEvent.Event.ShouldBe("content_block_delta");
        streamEvent.Data.ShouldBe("{\"a\":1}\n{\"b\":2}");
        streamEvent.Id.ShouldBe("evt-7");
        streamEvent.Retry.ShouldBe(3000);
    }

    [Fact]
    public void Constructor_WhenDataIsEmpty_IsAccepted()
    {
        var streamEvent = new ServerSentEvent(null, string.Empty, null, null);

        streamEvent.Data.ShouldBe(string.Empty);
    }

    [Fact]
    public void Equals_WhenAllMembersMatch_IsTrue()
    {
        var left = new ServerSentEvent("e", "d", "i", 1);
        var right = new ServerSentEvent("e", "d", "i", 1);

        left.ShouldBe(right);
        left.GetHashCode().ShouldBe(right.GetHashCode());
    }

    [Fact]
    public void WithExpression_WhenCloningWithNoChanges_ProducesEqualButDistinctInstance()
    {
        var original = new ServerSentEvent("e", "first", "i", 1);

        var copy = original with { };

        copy.ShouldNotBeSameAs(original);
        copy.ShouldBe(original);
        copy.Data.ShouldBe("first");
    }
}
