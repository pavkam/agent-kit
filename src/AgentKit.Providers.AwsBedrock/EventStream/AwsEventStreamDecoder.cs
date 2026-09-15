// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AwsBedrock.EventStream;

using System.Buffers.Binary;
using System.Globalization;

/// <summary>
/// Decodes the AWS event stream binary message framing
/// (<c>application/vnd.amazon.eventstream</c>) used by Bedrock Runtime's
/// <c>ConverseStream</c> response body.
/// </summary>
/// <remarks>
/// <para>
/// Each message is: a 12-byte prelude (4-byte total length, 4-byte headers
/// length, 4-byte prelude CRC), the headers block, the payload, and a
/// trailing 4-byte message CRC. Both CRCs use standard CRC-32 (IEEE
/// 802.3/GZIP).
/// </para>
/// <para>
/// The response body is untrusted input, so the decoder trusts nothing it
/// has not verified: it reads only the fixed-size prelude, verifies the
/// prelude CRC, enforces the AWS event-stream size limits
/// (<see cref="MaxHeadersLength"/>, <see cref="MaxPayloadLength"/>, and the
/// resulting <see cref="MaxTotalLength"/>) before allocating a buffer for
/// the rest of the frame, verifies the message CRC before interpreting a
/// single header byte, and bounds-checks every header field. Every
/// malformed shape surfaces as an explicit
/// <see cref="AwsEventStreamFormatException"/> rather than an out-of-memory
/// condition, an index fault, or silently misparsed content.
/// </para>
/// </remarks>
internal static class AwsEventStreamDecoder
{
    /// <summary>The largest headers block a frame may declare: 128 KiB.</summary>
    public const uint MaxHeadersLength = 128 * 1024;

    /// <summary>The largest payload a frame may carry: 16 MiB.</summary>
    public const uint MaxPayloadLength = 16 * 1024 * 1024;

    /// <summary>The largest total frame length: prelude, maximal headers, maximal payload, and message CRC.</summary>
    public const uint MaxTotalLength = _preludeLength + MaxHeadersLength + MaxPayloadLength + _messageCrcLength;

    private const int _preludeLength = 12;
    private const int _preludeCrcOffset = 8;
    private const int _messageCrcLength = 4;
    private const int _minimumTotalLength = _preludeLength + _messageCrcLength;
    private const int _headerLengthPrefixLength = 2;
    private const int _uuidHeaderLength = 16;

    /// <summary>
    /// Reads and decodes a single message from <paramref name="stream"/>.
    /// </summary>
    /// <param name="stream">The stream positioned at the start of a message, or at a clean end of stream.</param>
    /// <param name="cancellationToken">A token used to cancel the read.</param>
    /// <returns>The decoded message, or <see langword="null"/> if the stream ended cleanly before any message bytes.</returns>
    /// <exception cref="AwsEventStreamFormatException">
    /// The stream ended in the middle of a message, a checksum did not match,
    /// a declared length is impossible or exceeds the event-stream limits, or
    /// the headers block is malformed.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was signaled during a read.</exception>
    public static async Task<AwsEventStreamMessage?> ReadMessageAsync(Stream stream, CancellationToken cancellationToken)
    {
        Debug.Assert(stream is not null, "The parser always supplies the response body stream.");

        var prelude = new byte[_preludeLength];
        var preludeRead = await ReadUpToAsync(stream, prelude, cancellationToken).ConfigureAwait(false);
        if (preludeRead == 0)
        {
            return null;
        }

        if (preludeRead < _preludeLength)
        {
            throw new AwsEventStreamFormatException("The stream ended while reading a message prelude.");
        }

        var (totalLength, headersLength) = ValidatePrelude(prelude);

        // Only now is the declared length trusted enough to size a buffer.
        var frame = new byte[totalLength];
        prelude.CopyTo(frame, 0);
        await ReadExactAsync(stream, frame.AsMemory(_preludeLength), cancellationToken).ConfigureAwait(false);

        var messageCrc = BinaryPrimitives.ReadUInt32BigEndian(frame.AsSpan(frame.Length - _messageCrcLength, _messageCrcLength));
        if (Crc32.Compute(frame.AsSpan(0, frame.Length - _messageCrcLength)) != messageCrc)
        {
            throw new AwsEventStreamFormatException("The message checksum did not match.");
        }

        var headers = DecodeHeaders(frame.AsSpan(_preludeLength, headersLength));
        var payloadStart = _preludeLength + headersLength;
        var payloadLength = frame.Length - _messageCrcLength - payloadStart;
        var payload = frame.AsSpan(payloadStart, payloadLength).ToArray();

        return new AwsEventStreamMessage(headers, payload);
    }

    /// <summary>
    /// Verifies the prelude checksum and then validates the declared lengths against the framing invariants
    /// and the event-stream size limits, so no length is acted upon before it has been authenticated by
    /// the prelude CRC and bounded.
    /// </summary>
    /// <param name="prelude">The 12 prelude bytes.</param>
    /// <returns>The validated total frame length and headers-block length.</returns>
    /// <exception cref="AwsEventStreamFormatException">The prelude CRC mismatches or a declared length is invalid or out of bounds.</exception>
    private static (int TotalLength, int HeadersLength) ValidatePrelude(ReadOnlySpan<byte> prelude)
    {
        Debug.Assert(prelude.Length == _preludeLength, "The caller reads exactly the fixed-size prelude.");

        var declaredPreludeCrc = BinaryPrimitives.ReadUInt32BigEndian(prelude[_preludeCrcOffset..]);
        if (Crc32.Compute(prelude[.._preludeCrcOffset]) != declaredPreludeCrc)
        {
            throw new AwsEventStreamFormatException("The message prelude checksum did not match.");
        }

        var totalLength = BinaryPrimitives.ReadUInt32BigEndian(prelude);
        var headersLength = BinaryPrimitives.ReadUInt32BigEndian(prelude[4..]);

        if (totalLength < _minimumTotalLength)
        {
            throw new AwsEventStreamFormatException(
                $"A message declared an impossible total length of {totalLength.ToString(CultureInfo.InvariantCulture)} bytes.");
        }

        if (totalLength > MaxTotalLength)
        {
            throw new AwsEventStreamFormatException(
                $"A message declared a total length of {totalLength.ToString(CultureInfo.InvariantCulture)} bytes, exceeding the event-stream limit of {MaxTotalLength.ToString(CultureInfo.InvariantCulture)} bytes.");
        }

        if (headersLength > MaxHeadersLength)
        {
            throw new AwsEventStreamFormatException(
                $"A message declared a headers length of {headersLength.ToString(CultureInfo.InvariantCulture)} bytes, exceeding the event-stream limit of {MaxHeadersLength.ToString(CultureInfo.InvariantCulture)} bytes.");
        }

        if (headersLength > totalLength - _minimumTotalLength)
        {
            throw new AwsEventStreamFormatException("A message declared a headers length larger than the message itself.");
        }

        var payloadLength = totalLength - _minimumTotalLength - headersLength;
        return payloadLength > MaxPayloadLength
            ? throw new AwsEventStreamFormatException(
                $"A message declared a payload length of {payloadLength.ToString(CultureInfo.InvariantCulture)} bytes, exceeding the event-stream limit of {MaxPayloadLength.ToString(CultureInfo.InvariantCulture)} bytes.")
            : ((int) totalLength, (int) headersLength);
    }

    /// <summary>
    /// Decodes the headers block, bounds-checking every length before it is used to slice the block.
    /// </summary>
    /// <param name="headerBytes">The headers block, already covered by a verified message CRC.</param>
    /// <returns>The decoded headers keyed by name; a repeated name keeps its last value.</returns>
    /// <exception cref="AwsEventStreamFormatException">A header name, type, or value overruns the block or has an invalid shape.</exception>
    private static Dictionary<string, string> DecodeHeaders(ReadOnlySpan<byte> headerBytes)
    {
        var headers = new Dictionary<string, string>(StringComparer.Ordinal);
        var offset = 0;

        while (offset < headerBytes.Length)
        {
            var nameLength = headerBytes[offset];
            offset++;
            if (nameLength == 0)
            {
                throw new AwsEventStreamFormatException("A message header declared an empty name.");
            }

            if (headerBytes.Length - offset < nameLength)
            {
                throw new AwsEventStreamFormatException("A message header name overruns the headers block.");
            }

            var name = Encoding.UTF8.GetString(headerBytes.Slice(offset, nameLength));
            offset += nameLength;

            if (offset >= headerBytes.Length)
            {
                throw new AwsEventStreamFormatException("A message header ends before its value type.");
            }

            var valueType = headerBytes[offset];
            offset++;

            var (value, consumed) = DecodeHeaderValue(valueType, headerBytes[offset..]);
            offset += consumed;

            headers[name] = value;
        }

        return headers;
    }

    /// <summary>Decodes one header value of the given wire type from the start of <paramref name="remaining"/>.</summary>
    /// <param name="valueType">The header value type byte.</param>
    /// <param name="remaining">The headers block from the first value byte onward.</param>
    /// <returns>The value's string projection and the number of bytes it occupied.</returns>
    /// <exception cref="AwsEventStreamFormatException">The type is unrecognized or the value overruns the headers block.</exception>
    private static (string Value, int Consumed) DecodeHeaderValue(byte valueType, ReadOnlySpan<byte> remaining) =>
        valueType switch
        {
            0 => ("true", 0),
            1 => ("false", 0),
            2 => (((sbyte) RequireFixed(remaining, 1)[0]).ToString(CultureInfo.InvariantCulture), 1),
            3 => (BinaryPrimitives.ReadInt16BigEndian(RequireFixed(remaining, 2)).ToString(CultureInfo.InvariantCulture), 2),
            4 => (BinaryPrimitives.ReadInt32BigEndian(RequireFixed(remaining, 4)).ToString(CultureInfo.InvariantCulture), 4),
            5 => (BinaryPrimitives.ReadInt64BigEndian(RequireFixed(remaining, 8)).ToString(CultureInfo.InvariantCulture), 8),
            6 => DecodeLengthPrefixed(remaining, static bytes => Convert.ToBase64String(bytes)),
            7 => DecodeLengthPrefixed(remaining, static bytes => Encoding.UTF8.GetString(bytes)),
            8 => (BinaryPrimitives.ReadInt64BigEndian(RequireFixed(remaining, 8)).ToString(CultureInfo.InvariantCulture), 8),
            9 => (Convert.ToHexStringLower(RequireFixed(remaining, _uuidHeaderLength)), _uuidHeaderLength),
            _ => throw new AwsEventStreamFormatException($"Unrecognized event stream header value type '{valueType.ToString(CultureInfo.InvariantCulture)}'."),
        };

    /// <summary>Returns the first <paramref name="length"/> bytes of a fixed-size header value, or fails when the block is too short.</summary>
    /// <param name="remaining">The headers block from the first value byte onward.</param>
    /// <param name="length">The fixed value length the type requires.</param>
    /// <returns>The value bytes.</returns>
    /// <exception cref="AwsEventStreamFormatException">Fewer than <paramref name="length"/> bytes remain.</exception>
    private static ReadOnlySpan<byte> RequireFixed(ReadOnlySpan<byte> remaining, int length)
    {
        Debug.Assert(length > 0, "Fixed-size header values are never empty.");

        return remaining.Length < length
            ? throw new AwsEventStreamFormatException("A message header value overruns the headers block.")
            : remaining[..length];
    }

    /// <summary>Decodes a 16-bit-length-prefixed header value, or fails when the prefix or the value overruns the block.</summary>
    /// <param name="remaining">The headers block from the first value byte onward.</param>
    /// <param name="project">Projects the value bytes to their string form.</param>
    /// <returns>The projected value and the number of bytes consumed including the prefix.</returns>
    /// <exception cref="AwsEventStreamFormatException">The length prefix or the value overruns the headers block.</exception>
    private static (string Value, int Consumed) DecodeLengthPrefixed(ReadOnlySpan<byte> remaining, Func<byte[], string> project)
    {
        if (remaining.Length < _headerLengthPrefixLength)
        {
            throw new AwsEventStreamFormatException("A message header value length overruns the headers block.");
        }

        var length = BinaryPrimitives.ReadUInt16BigEndian(remaining);
        if (remaining.Length - _headerLengthPrefixLength < length)
        {
            throw new AwsEventStreamFormatException("A message header value overruns the headers block.");
        }

        var bytes = remaining.Slice(_headerLengthPrefixLength, length).ToArray();
        return (project(bytes), _headerLengthPrefixLength + length);
    }

    private static async Task<int> ReadUpToAsync(Stream stream, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer[total..], cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return total;
    }

    private static async Task ReadExactAsync(Stream stream, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var read = await ReadUpToAsync(stream, buffer, cancellationToken).ConfigureAwait(false);
        if (read < buffer.Length)
        {
            throw new AwsEventStreamFormatException("The stream ended in the middle of a message.");
        }
    }
}
