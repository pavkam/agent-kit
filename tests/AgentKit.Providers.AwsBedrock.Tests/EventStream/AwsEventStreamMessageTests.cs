// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AwsBedrock.Tests.EventStream;

using AgentKit.Providers.AwsBedrock.EventStream;

/// <summary>Verifies the argument constraints and value semantics of <see cref="AwsEventStreamMessage"/>.</summary>
public sealed class AwsEventStreamMessageTests
{
    [Fact]
    public void Constructor_WhenHeadersIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new AwsEventStreamMessage(null!, []));

        exception.ParamName.ShouldBe("headers");
    }

    [Fact]
    public void Constructor_WhenPayloadIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new AwsEventStreamMessage(new Dictionary<string, string>(), null!));

        exception.ParamName.ShouldBe("payload");
    }

    [Fact]
    public void Constructor_WhenValuesSupplied_ExposesThemUnchanged()
    {
        var headers = new Dictionary<string, string> { [":message-type"] = "event" };
        var payload = new byte[] { 1, 2, 3 };

        var message = new AwsEventStreamMessage(headers, payload);

        message.Headers.ShouldBeSameAs(headers);
        message.Payload.ShouldBeSameAs(payload);
    }

    [Fact]
    public void MessageType_WhenHeaderIsPresent_ReturnsValue()
    {
        var message = new AwsEventStreamMessage(
            new Dictionary<string, string> { [":message-type"] = "event" }, []);

        message.MessageType.ShouldBe("event");
    }

    [Fact]
    public void MessageType_WhenHeaderIsAbsent_ReturnsNull()
    {
        var message = new AwsEventStreamMessage(new Dictionary<string, string>(), []);

        message.MessageType.ShouldBeNull();
    }

    [Fact]
    public void EventType_WhenHeaderIsPresent_ReturnsValue()
    {
        var message = new AwsEventStreamMessage(
            new Dictionary<string, string> { [":event-type"] = "messageStart" }, []);

        message.EventType.ShouldBe("messageStart");
    }

    [Fact]
    public void EventType_WhenHeaderIsAbsent_ReturnsNull()
    {
        var message = new AwsEventStreamMessage(new Dictionary<string, string>(), []);

        message.EventType.ShouldBeNull();
    }

    [Fact]
    public void ExceptionType_WhenHeaderIsPresent_ReturnsValue()
    {
        var message = new AwsEventStreamMessage(
            new Dictionary<string, string> { [":exception-type"] = "ThrottlingException" }, []);

        message.ExceptionType.ShouldBe("ThrottlingException");
    }

    [Fact]
    public void ExceptionType_WhenHeaderIsAbsent_ReturnsNull()
    {
        var message = new AwsEventStreamMessage(new Dictionary<string, string>(), []);

        message.ExceptionType.ShouldBeNull();
    }

    [Fact]
    public void Equals_WhenAllMembersMatch_IsTrue()
    {
        var headers = new Dictionary<string, string> { [":message-type"] = "event" };
        var payload = new byte[] { 1, 2, 3 };
        var left = new AwsEventStreamMessage(headers, payload);
        var right = new AwsEventStreamMessage(headers, payload);

        left.ShouldBe(right);
        left.GetHashCode().ShouldBe(right.GetHashCode());
    }

    [Fact]
    public void WithExpression_WhenCloningWithNoChanges_ProducesEqualButDistinctInstance()
    {
        var headers = new Dictionary<string, string> { [":message-type"] = "event" };
        var payload = new byte[] { 1, 2, 3 };
        var original = new AwsEventStreamMessage(headers, payload);

        var copy = original with { };

        copy.ShouldNotBeSameAs(original);
        copy.ShouldBe(original);
        copy.Headers.ShouldBeSameAs(original.Headers);
    }
}
