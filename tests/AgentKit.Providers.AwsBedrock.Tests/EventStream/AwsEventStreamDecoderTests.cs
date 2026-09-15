// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AwsBedrock.Tests.EventStream;

using AgentKit.Providers.AwsBedrock.EventStream;
using AgentKit.Providers.AwsBedrock.Tests.Fakes;

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
    public void EncodeEvent_WhenTestEncoderFramesMessageStart_ReproducesIndependentReferenceBytes()
    {
        // Pins the in-test encoder to the independently generated reference framing so scenarios built
        // from it exercise the decoder against real wire bytes rather than a self-consistent loop.
        var encoded = AwsEventStreamTestEncoder.EncodeEvent("messageStart", /*lang=json,strict*/ """{"role":"assistant"}""");

        Convert.ToHexStringLower(encoded).ShouldBe(_messageStartHex);
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

    [Fact]
    public async Task ReadMessageAsync_WhenTotalLengthExceedsLimit_ThrowsFormatExceptionWithoutAllocating()
    {
        // A valid prelude CRC over a hostile 4 GiB total length: the decoder must reject the declared
        // length before sizing any buffer and before reading a single body byte.
        var frame = AwsEventStreamTestEncoder.EncodeRawFrame(
            AwsEventStreamTestEncoder.EncodeStringHeaders([new(":message-type", "event")]),
            "{}"u8,
            totalLength: 0xFFFF_FFF0);
        await using var stream = new MemoryStream(frame);
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();

        var exception = await Should.ThrowAsync<AwsEventStreamFormatException>(
            () => AwsEventStreamDecoder.ReadMessageAsync(stream, TestContext.Current.CancellationToken));

        exception.Message.ShouldContain("exceeding the event-stream limit");
        stream.Position.ShouldBe(12);
        (GC.GetAllocatedBytesForCurrentThread() - allocatedBefore).ShouldBeLessThan(1024 * 1024);
    }

    [Theory]
    [InlineData(AwsEventStreamDecoder.MaxTotalLength + 1)]
    [InlineData(uint.MaxValue)]
    public async Task ReadMessageAsync_WhenTotalLengthIsAboveMaximum_ThrowsFormatException(uint totalLength)
    {
        var frame = AwsEventStreamTestEncoder.EncodeRawFrame([], "{}"u8, totalLength: totalLength);
        await using var stream = new MemoryStream(frame);

        var exception = await Should.ThrowAsync<AwsEventStreamFormatException>(
            () => AwsEventStreamDecoder.ReadMessageAsync(stream, TestContext.Current.CancellationToken));

        exception.Message.ShouldContain("total length");
    }

    [Fact]
    public async Task ReadMessageAsync_WhenHeadersLengthExceedsMaximum_ThrowsFormatException()
    {
        // Total length is consistent with the oversized headers block, so only the headers limit rejects it.
        const uint headersLength = AwsEventStreamDecoder.MaxHeadersLength + 1;
        var frame = AwsEventStreamTestEncoder.EncodeRawFrame([], [], totalLength: 16 + headersLength, headersLength: headersLength);
        await using var stream = new MemoryStream(frame);

        var exception = await Should.ThrowAsync<AwsEventStreamFormatException>(
            () => AwsEventStreamDecoder.ReadMessageAsync(stream, TestContext.Current.CancellationToken));

        exception.Message.ShouldContain("headers length");
        stream.Position.ShouldBe(12);
    }

    [Fact]
    public async Task ReadMessageAsync_WhenPayloadLengthExceedsMaximum_ThrowsFormatException()
    {
        // Headers fit and the total is under the absolute ceiling, but the implied payload is over 16 MiB.
        const uint totalLength = 16 + AwsEventStreamDecoder.MaxPayloadLength + 1;
        var frame = AwsEventStreamTestEncoder.EncodeRawFrame([], [], totalLength: totalLength, headersLength: 0);
        await using var stream = new MemoryStream(frame);

        var exception = await Should.ThrowAsync<AwsEventStreamFormatException>(
            () => AwsEventStreamDecoder.ReadMessageAsync(stream, TestContext.Current.CancellationToken));

        exception.Message.ShouldContain("payload length");
        stream.Position.ShouldBe(12);
    }

    [Fact]
    public async Task ReadMessageAsync_WhenPreludeCrcMismatches_ThrowsBeforeReadingBody()
    {
        var bytes = Convert.FromHexString(_messageStartHex);
        bytes[9] ^= 0xFF; // corrupt the prelude CRC itself; the declared lengths remain plausible
        await using var stream = new MemoryStream(bytes);

        var exception = await Should.ThrowAsync<AwsEventStreamFormatException>(
            () => AwsEventStreamDecoder.ReadMessageAsync(stream, TestContext.Current.CancellationToken));

        exception.Message.ShouldBe("The message prelude checksum did not match.");
        stream.Position.ShouldBe(12);
    }

    [Fact]
    public async Task ReadMessageAsync_WhenPreludeDeclaresOversizeLengthWithBadCrc_ReportsCrcMismatchFirst()
    {
        // With an unauthenticated prelude the lengths are meaningless; the CRC failure is the truthful cause.
        var frame = AwsEventStreamTestEncoder.EncodeRawFrame([], "{}"u8, totalLength: uint.MaxValue, preludeCrc: 0xDEADBEEF);
        await using var stream = new MemoryStream(frame);

        var exception = await Should.ThrowAsync<AwsEventStreamFormatException>(
            () => AwsEventStreamDecoder.ReadMessageAsync(stream, TestContext.Current.CancellationToken));

        exception.Message.ShouldBe("The message prelude checksum did not match.");
        stream.Position.ShouldBe(12);
    }

    [Fact]
    public async Task ReadMessageAsync_WhenMessageCrcMismatches_ThrowsBeforeDecodingHeaders()
    {
        // The headers block is deliberately malformed (a string value claiming 0xFFFF bytes) AND the message
        // CRC is wrong: the CRC failure must be reported, proving no header byte was interpreted first.
        byte[] malformedHeaders = [3, (byte) ':', (byte) 'a', (byte) 'b', 7, 0xFF, 0xFF, 0x00];
        var frame = AwsEventStreamTestEncoder.EncodeRawFrame(malformedHeaders, "{}"u8, messageCrc: 0x01020304);
        await using var stream = new MemoryStream(frame);

        var exception = await Should.ThrowAsync<AwsEventStreamFormatException>(
            () => AwsEventStreamDecoder.ReadMessageAsync(stream, TestContext.Current.CancellationToken));

        exception.Message.ShouldBe("The message checksum did not match.");
    }

    [Fact]
    public async Task ReadMessageAsync_WhenHeadersLengthOverrunsFrame_ThrowsFormatException()
    {
        // A CRC-valid prelude whose headers length leaves no room for the message CRC is rejected before allocation.
        var headers = AwsEventStreamTestEncoder.EncodeStringHeaders([new(":message-type", "event")]);
        var trueTotal = (uint) (12 + headers.Length + 2 + 4);
        var frame = AwsEventStreamTestEncoder.EncodeRawFrame(headers, "{}"u8, headersLength: trueTotal - 15);
        await using var stream = new MemoryStream(frame);

        var exception = await Should.ThrowAsync<AwsEventStreamFormatException>(
            () => AwsEventStreamDecoder.ReadMessageAsync(stream, TestContext.Current.CancellationToken));

        exception.Message.ShouldBe("A message declared a headers length larger than the message itself.");
        stream.Position.ShouldBe(12);
    }

    public static TheoryData<string, byte[]> MalformedHeaderBlocks => new()
    {
        { "string value length overruns block", [3, (byte) ':', (byte) 'a', (byte) 'b', 7, 0x00, 0x10, (byte) 'x'] },
        { "string value length prefix truncated", [3, (byte) ':', (byte) 'a', (byte) 'b', 7, 0x00] },
        { "name length overruns block", [9, (byte) ':', (byte) 'a', (byte) 'b'] },
        { "name ends before value type", [2, (byte) ':', (byte) 'a'] },
        { "empty header name", [0, 7, 0x00, 0x00] },
        { "int32 value truncated", [1, (byte) 'n', 4, 0x00, 0x01] },
        { "int64 value truncated", [1, (byte) 'n', 5, 0x00, 0x01, 0x02] },
        { "timestamp value truncated", [1, (byte) 't', 8, 0x00] },
        { "uuid value truncated", [1, (byte) 'u', 9, 0x00, 0x01, 0x02, 0x03] },
        { "byte-array value length overruns block", [1, (byte) 'b', 6, 0x00, 0x08, 0x01] },
        { "unknown value type", [1, (byte) 'z', 42] },
    };

    [Theory]
    [MemberData(nameof(MalformedHeaderBlocks))]
    public async Task ReadMessageAsync_WhenHeaderFieldOverrunsHeadersBlock_ThrowsFormatException(string scenario, byte[] headersBlock)
    {
        // Every frame here carries correct lengths and CRCs; only the header encoding itself is malformed.
        _ = scenario;
        var frame = AwsEventStreamTestEncoder.EncodeRawFrame(headersBlock, "{}"u8);
        await using var stream = new MemoryStream(frame);

        _ = await Should.ThrowAsync<AwsEventStreamFormatException>(
            () => AwsEventStreamDecoder.ReadMessageAsync(stream, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReadMessageAsync_WhenHeadersUseEveryValueType_DecodesEachProjection()
    {
        byte[] headersBlock =
        [
            1, (byte) 't', 0,
            1, (byte) 'f', 1,
            1, (byte) 'b', 2, 0xFE,
            1, (byte) 's', 3, 0xFF, 0xFE,
            1, (byte) 'i', 4, 0x00, 0x00, 0x01, 0x00,
            1, (byte) 'l', 5, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x2A,
            1, (byte) 'a', 6, 0x00, 0x03, 0x01, 0x02, 0x03,
            1, (byte) 'u', 7, 0x00, 0x02, (byte) 'o', (byte) 'k',
            1, (byte) 'd', 8, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x07,
            1, (byte) 'g', 9, 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F,
        ];
        var frame = AwsEventStreamTestEncoder.EncodeRawFrame(headersBlock, []);
        await using var stream = new MemoryStream(frame);

        var message = await AwsEventStreamDecoder.ReadMessageAsync(stream, TestContext.Current.CancellationToken);

        _ = message.ShouldNotBeNull();
        message.Headers["t"].ShouldBe("true");
        message.Headers["f"].ShouldBe("false");
        message.Headers["b"].ShouldBe("-2");
        message.Headers["s"].ShouldBe("-2");
        message.Headers["i"].ShouldBe("256");
        message.Headers["l"].ShouldBe("42");
        message.Headers["a"].ShouldBe("AQID");
        message.Headers["u"].ShouldBe("ok");
        message.Headers["d"].ShouldBe("7");
        message.Headers["g"].ShouldBe("000102030405060708090a0b0c0d0e0f");
        message.Payload.ShouldBeEmpty();
    }

    [Fact]
    public async Task ReadMessageAsync_WhenPayloadIsEmptyAndHeadersAreEmpty_DecodesMinimalFrame()
    {
        var frame = AwsEventStreamTestEncoder.EncodeRawFrame([], []);
        frame.Length.ShouldBe(16);
        await using var stream = new MemoryStream(frame);

        var message = await AwsEventStreamDecoder.ReadMessageAsync(stream, TestContext.Current.CancellationToken);

        _ = message.ShouldNotBeNull();
        message.Headers.ShouldBeEmpty();
        message.Payload.ShouldBeEmpty();
    }

    [Fact]
    public async Task ReadMessageAsync_WhenTotalLengthIsBelowMinimum_ThrowsFormatException()
    {
        var frame = AwsEventStreamTestEncoder.EncodeRawFrame([], [], totalLength: 15);
        await using var stream = new MemoryStream(frame);

        var exception = await Should.ThrowAsync<AwsEventStreamFormatException>(
            () => AwsEventStreamDecoder.ReadMessageAsync(stream, TestContext.Current.CancellationToken));

        exception.Message.ShouldContain("impossible total length");
    }

    [Fact]
    public async Task ReadMessageAsync_WhenStreamEndsInsidePrelude_ThrowsFormatException()
    {
        await using var stream = new MemoryStream(Convert.FromHexString(_messageStartHex)[..7]);

        var exception = await Should.ThrowAsync<AwsEventStreamFormatException>(
            () => AwsEventStreamDecoder.ReadMessageAsync(stream, TestContext.Current.CancellationToken));

        exception.Message.ShouldBe("The stream ended while reading a message prelude.");
    }

    [Fact]
    public async Task ReadMessageAsync_WhenCancelledBeforeRead_ThrowsOperationCanceled()
    {
        await using var stream = new MemoryStream(Convert.FromHexString(_messageStartHex));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => AwsEventStreamDecoder.ReadMessageAsync(stream, cancellation.Token));
    }
}
