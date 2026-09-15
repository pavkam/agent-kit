// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AwsBedrock.Tests.Fakes;

using System.Buffers.Binary;

using AgentKit.Providers.AwsBedrock.EventStream;

/// <summary>
/// Encodes AWS event stream (<c>application/vnd.amazon.eventstream</c>)
/// frames for tests, so streaming scenarios can be assembled from typed
/// events in test source instead of hand-edited binary. Every frame follows
/// the documented framing: a 12-byte prelude (total length, headers length,
/// prelude CRC-32), the headers block, the payload, and the trailing
/// message CRC-32 over everything before it.
/// </summary>
/// <remarks>
/// The well-formed encoders mirror the shape Bedrock Runtime emits for
/// <c>ConverseStream</c>: three string headers (<c>:message-type</c>,
/// <c>:event-type</c> or <c>:exception-type</c>, and <c>:content-type</c>)
/// followed by a JSON payload. <see cref="EncodeRawFrame"/> additionally
/// lets a test override each prelude field and checksum independently to
/// build deliberately malformed frames.
/// </remarks>
internal static class AwsEventStreamTestEncoder
{
    private const byte _stringHeaderValueType = 7;

    /// <summary>Encodes one <c>event</c> frame with a JSON payload, as Bedrock emits for a <c>ConverseStream</c> event.</summary>
    /// <param name="eventType">The <c>:event-type</c> header value, such as <c>contentBlockDelta</c>.</param>
    /// <param name="jsonPayload">The JSON payload text.</param>
    /// <returns>The complete framed message bytes.</returns>
    public static byte[] EncodeEvent(string eventType, string jsonPayload) =>
        EncodeMessage(
            [
                new(":message-type", "event"),
                new(":event-type", eventType),
                new(":content-type", "application/json"),
            ],
            Encoding.UTF8.GetBytes(jsonPayload));

    /// <summary>Encodes one <c>exception</c> frame with a JSON payload, as Bedrock emits for a mid-stream error.</summary>
    /// <param name="exceptionType">The <c>:exception-type</c> header value, such as <c>throttlingException</c>.</param>
    /// <param name="jsonPayload">The JSON payload text.</param>
    /// <returns>The complete framed message bytes.</returns>
    public static byte[] EncodeException(string exceptionType, string jsonPayload) =>
        EncodeMessage(
            [
                new(":message-type", "exception"),
                new(":exception-type", exceptionType),
                new(":content-type", "application/json"),
            ],
            Encoding.UTF8.GetBytes(jsonPayload));

    /// <summary>Encodes one well-formed frame from string headers and an arbitrary payload.</summary>
    /// <param name="headers">The string headers, in the order they should be written.</param>
    /// <param name="payload">The raw payload bytes.</param>
    /// <returns>The complete framed message bytes with correct lengths and checksums.</returns>
    public static byte[] EncodeMessage(IReadOnlyList<KeyValuePair<string, string>> headers, ReadOnlySpan<byte> payload) =>
        EncodeRawFrame(EncodeStringHeaders(headers), payload);

    /// <summary>Encodes a headers block consisting of the given string (type 7) headers.</summary>
    /// <param name="headers">The string headers, in the order they should be written.</param>
    /// <returns>The encoded headers block bytes.</returns>
    public static byte[] EncodeStringHeaders(IReadOnlyList<KeyValuePair<string, string>> headers)
    {
        using var buffer = new MemoryStream();
        Span<byte> valueLength = stackalloc byte[2];
        foreach (var (name, value) in headers)
        {
            var nameBytes = Encoding.UTF8.GetBytes(name);
            var valueBytes = Encoding.UTF8.GetBytes(value);
            buffer.WriteByte(checked((byte) nameBytes.Length));
            buffer.Write(nameBytes);
            buffer.WriteByte(_stringHeaderValueType);
            BinaryPrimitives.WriteUInt16BigEndian(valueLength, checked((ushort) valueBytes.Length));
            buffer.Write(valueLength);
            buffer.Write(valueBytes);
        }

        return buffer.ToArray();
    }

    /// <summary>
    /// Encodes one frame from a pre-built headers block and payload, computing the prelude and message
    /// checksums from the bytes actually written unless a test overrides a field to build a malformed frame.
    /// </summary>
    /// <param name="headersBlock">The encoded headers block bytes.</param>
    /// <param name="payload">The raw payload bytes.</param>
    /// <param name="totalLength">Overrides the declared total length; defaults to the true frame length.</param>
    /// <param name="headersLength">Overrides the declared headers length; defaults to <paramref name="headersBlock"/>'s length.</param>
    /// <param name="preludeCrc">Overrides the prelude CRC; defaults to the correct CRC over the declared lengths.</param>
    /// <param name="messageCrc">Overrides the message CRC; defaults to the correct CRC over the frame before it.</param>
    /// <returns>The framed bytes; their length is always the true length regardless of <paramref name="totalLength"/>.</returns>
    public static byte[] EncodeRawFrame(
        ReadOnlySpan<byte> headersBlock,
        ReadOnlySpan<byte> payload,
        uint? totalLength = null,
        uint? headersLength = null,
        uint? preludeCrc = null,
        uint? messageCrc = null)
    {
        var frame = new byte[12 + headersBlock.Length + payload.Length + 4];
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(0, 4), totalLength ?? (uint) frame.Length);
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(4, 4), headersLength ?? (uint) headersBlock.Length);
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(8, 4), preludeCrc ?? Crc32.Compute(frame.AsSpan(0, 8)));
        headersBlock.CopyTo(frame.AsSpan(12));
        payload.CopyTo(frame.AsSpan(12 + headersBlock.Length));
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(frame.Length - 4), messageCrc ?? Crc32.Compute(frame.AsSpan(0, frame.Length - 4)));
        return frame;
    }

    /// <summary>Concatenates frames into one contiguous response body.</summary>
    /// <param name="frames">The frames, in wire order.</param>
    /// <returns>The concatenated bytes.</returns>
    public static byte[] Concat(params IEnumerable<byte[]> frames) => [.. frames.SelectMany(static frame => frame)];
}
