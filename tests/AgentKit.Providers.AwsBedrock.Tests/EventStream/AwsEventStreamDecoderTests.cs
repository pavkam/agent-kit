// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AwsBedrock.Tests.EventStream;

using AgentKit.Providers.AwsBedrock.EventStream;

/// <summary>
/// Verifies <see cref="AwsEventStreamDecoder"/> against binary fixtures
/// independently generated with a Python <c>struct</c>/<c>zlib.crc32</c>
/// reference encoder implementing the same documented AWS event stream
/// binary framing.
/// </summary>
public sealed class AwsEventStreamDecoderTests
{
    // Encodes {":message-type":"event", ":event-type":"messageStart", ":content-type":"application/json"}
    // with payload {"role":"assistant"}.
    private const string _messageStartHex =
        "000000760000005296d5fade0d3a6d6573736167652d747970650700056576656e740b3a6576656e742d747970650700" +
        "0c6d65737361676553746172740d3a636f6e74656e742d747970650700106170706c69636174696f6e2f6a736f6e7b2272" +
        "6f6c65223a22617373697374616e74227d1cc6be18";

    // Encodes {":message-type":"event", ":event-type":"contentBlockDelta", ":content-type":"application/json"}
    // with payload {"contentBlockIndex":0,"delta":{"text":"Hello"}}.
    private const string _contentBlockDeltaHex =
        "0000009700000057f30be03e0d3a6d6573736167652d747970650700056576656e740b3a6576656e742d747970650700" +
        "11636f6e74656e74426c6f636b44656c74610d3a636f6e74656e742d747970650700106170706c69636174696f6e2f6a73" +
        "6f6e7b22636f6e74656e74426c6f636b496e646578223a302c2264656c7461223a7b2274657874223a2248656c6c6f227d" +
        "7dd3df8db4";

    // Encodes {":message-type":"exception", ":exception-type":"throttlingException", ":content-type":"application/json"}
    // with payload {"message":"Too many requests"}.
    private const string _throttlingExceptionHex =
        "00000090000000618e91a9b70d3a6d6573736167652d74797065070009657863657074696f6e0f3a657863657074696f" +
        "6e2d747970650700137468726f74746c696e67457863657074696f6e0d3a636f6e74656e742d747970650700106170706c" +
        "69636174696f6e2f6a736f6e7b226d657373616765223a22546f6f206d616e79207265717565737473227d9d6b6211";

    [Fact]
    public async Task ReadMessageAsync_WhenMessageStartEvent_DecodesHeadersAndPayload()
    {
        await using var stream = new MemoryStream(Convert.FromHexString(_messageStartHex));

        var message = await AwsEventStreamDecoder.ReadMessageAsync(stream, TestContext.Current.CancellationToken);

        _ = message.ShouldNotBeNull();
        message.MessageType.ShouldBe("event");
        message.EventType.ShouldBe("messageStart");
        message.Headers[":content-type"].ShouldBe("application/json");
        Encoding.UTF8.GetString(message.Payload).ShouldBe(/*lang=json,strict*/ """{"role":"assistant"}""");
    }

    [Fact]
    public async Task ReadMessageAsync_WhenContentBlockDeltaEvent_DecodesHeadersAndPayload()
    {
        await using var stream = new MemoryStream(Convert.FromHexString(_contentBlockDeltaHex));

        var message = await AwsEventStreamDecoder.ReadMessageAsync(stream, TestContext.Current.CancellationToken);

        _ = message.ShouldNotBeNull();
        message.EventType.ShouldBe("contentBlockDelta");
        Encoding.UTF8.GetString(message.Payload).ShouldBe(/*lang=json,strict*/ """{"contentBlockIndex":0,"delta":{"text":"Hello"}}""");
    }

    [Fact]
    public async Task ReadMessageAsync_WhenExceptionEvent_DecodesExceptionTypeHeader()
    {
        await using var stream = new MemoryStream(Convert.FromHexString(_throttlingExceptionHex));

        var message = await AwsEventStreamDecoder.ReadMessageAsync(stream, TestContext.Current.CancellationToken);

        _ = message.ShouldNotBeNull();
        message.MessageType.ShouldBe("exception");
        message.ExceptionType.ShouldBe("throttlingException");
        Encoding.UTF8.GetString(message.Payload).ShouldBe(/*lang=json,strict*/ """{"message":"Too many requests"}""");
    }

    [Fact]
    public async Task ReadMessageAsync_WhenMultipleMessagesConcatenated_DecodesEachInOrder()
    {
        var combined = Convert.FromHexString(_messageStartHex).Concat(Convert.FromHexString(_contentBlockDeltaHex)).ToArray();
        await using var stream = new MemoryStream(combined);

        var first = await AwsEventStreamDecoder.ReadMessageAsync(stream, TestContext.Current.CancellationToken);
        var second = await AwsEventStreamDecoder.ReadMessageAsync(stream, TestContext.Current.CancellationToken);
        var third = await AwsEventStreamDecoder.ReadMessageAsync(stream, TestContext.Current.CancellationToken);

        first.ShouldNotBeNull().EventType.ShouldBe("messageStart");
        second.ShouldNotBeNull().EventType.ShouldBe("contentBlockDelta");
        third.ShouldBeNull();
    }

    [Fact]
    public async Task ReadMessageAsync_WhenStreamIsEmpty_ReturnsNull()
    {
        await using var stream = new MemoryStream([]);

        var message = await AwsEventStreamDecoder.ReadMessageAsync(stream, TestContext.Current.CancellationToken);

        message.ShouldBeNull();
    }

    [Fact]
    public async Task ReadMessageAsync_WhenPayloadByteIsCorrupted_ThrowsFormatException()
    {
        var bytes = Convert.FromHexString(_messageStartHex);
        bytes[^10] ^= 0xFF; // flip a byte inside the payload region
        await using var stream = new MemoryStream(bytes);

        _ = await Should.ThrowAsync<AwsEventStreamFormatException>(
            () => AwsEventStreamDecoder.ReadMessageAsync(stream, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReadMessageAsync_WhenPreludeIsCorrupted_ThrowsFormatException()
    {
        var bytes = Convert.FromHexString(_messageStartHex);
        bytes[4] ^= 0xFF; // flip a byte inside the headers-length prelude field
        await using var stream = new MemoryStream(bytes);

        _ = await Should.ThrowAsync<AwsEventStreamFormatException>(
            () => AwsEventStreamDecoder.ReadMessageAsync(stream, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReadMessageAsync_WhenStreamEndsMidMessage_ThrowsFormatException()
    {
        var bytes = Convert.FromHexString(_messageStartHex);
        await using var stream = new MemoryStream(bytes[..(bytes.Length - 20)]);

        _ = await Should.ThrowAsync<AwsEventStreamFormatException>(
            () => AwsEventStreamDecoder.ReadMessageAsync(stream, TestContext.Current.CancellationToken));
    }
}
