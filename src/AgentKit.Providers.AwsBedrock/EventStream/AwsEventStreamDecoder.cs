// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AwsBedrock.EventStream;

using System.Buffers.Binary;

/// <summary>
/// Decodes the AWS event stream binary message framing
/// (<c>application/vnd.amazon.eventstream</c>) used by Bedrock Runtime's
/// <c>ConverseStream</c> response body.
/// </summary>
/// <remarks>
/// Each message is: a 12-byte prelude (4-byte total length, 4-byte headers
/// length, 4-byte prelude CRC), the headers block, the payload, and a
/// trailing 4-byte message CRC. Both CRCs use standard CRC-32 (IEEE
/// 802.3/GZIP). This decoder validates both checksums and never returns a
/// message whose checksum does not match, so a corrupted frame is
/// surfaced as an explicit <see cref="AwsEventStreamFormatException"/>
/// rather than silently misparsed content.
/// </remarks>
internal static class AwsEventStreamDecoder
{
    private const int _preludeFieldsLength = 8;
    private const int _preludeTotalLength = 12;

    /// <summary>
    /// Reads and decodes a single message from <paramref name="stream"/>.
    /// </summary>
    /// <param name="stream">The stream positioned at the start of a message, or at a clean end of stream.</param>
    /// <param name="cancellationToken">A token used to cancel the read.</param>
    /// <returns>The decoded message, or <see langword="null"/> if the stream ended cleanly before any message bytes.</returns>
    /// <exception cref="AwsEventStreamFormatException">
    /// The stream ended in the middle of a message, or a checksum did not match.
    /// </exception>
    public static async Task<AwsEventStreamMessage?> ReadMessageAsync(Stream stream, CancellationToken cancellationToken)
    {
        var totalLengthBytes = new byte[4];
        var firstRead = await ReadUpToAsync(stream, totalLengthBytes, cancellationToken).ConfigureAwait(false);
        if (firstRead == 0)
        {
            return null;
        }

        if (firstRead < 4)
        {
            throw new AwsEventStreamFormatException("The stream ended while reading a message's total-length prelude field.");
        }

        var totalLength = BinaryPrimitives.ReadUInt32BigEndian(totalLengthBytes);
        if (totalLength < _preludeTotalLength)
        {
            throw new AwsEventStreamFormatException($"A message declared an impossible total length of {totalLength} bytes.");
        }

        var rest = new byte[totalLength - 4];
        await ReadExactAsync(stream, rest, cancellationToken).ConfigureAwait(false);

        var headersLength = BinaryPrimitives.ReadUInt32BigEndian(rest.AsSpan(0, 4));
        var preludeCrc = BinaryPrimitives.ReadUInt32BigEndian(rest.AsSpan(4, 4));

        Span<byte> preludeBytes = stackalloc byte[_preludeFieldsLength];
        totalLengthBytes.CopyTo(preludeBytes[..4]);
        rest.AsSpan(0, 4).CopyTo(preludeBytes[4..]);
        if (Crc32.Compute(preludeBytes) != preludeCrc)
        {
            throw new AwsEventStreamFormatException("The message prelude checksum did not match.");
        }

        var headersStart = 8;
        if (headersStart + headersLength + 4 > rest.Length)
        {
            throw new AwsEventStreamFormatException("A message declared a headers length larger than the message itself.");
        }

        var headers = DecodeHeaders(rest.AsSpan(headersStart, (int) headersLength));

        var payloadStart = headersStart + (int) headersLength;
        var payloadLength = rest.Length - 4 - payloadStart;
        var payload = rest.AsSpan(payloadStart, payloadLength).ToArray();

        var messageCrc = BinaryPrimitives.ReadUInt32BigEndian(rest.AsSpan(rest.Length - 4, 4));
        var messageCrcInput = new byte[4 + rest.Length - 4];
        totalLengthBytes.CopyTo(messageCrcInput.AsSpan(0, 4));
        rest.AsSpan(0, rest.Length - 4).CopyTo(messageCrcInput.AsSpan(4));
        return Crc32.Compute(messageCrcInput) != messageCrc
            ? throw new AwsEventStreamFormatException("The message checksum did not match.")
            : new AwsEventStreamMessage(headers, payload);
    }

    private static Dictionary<string, string> DecodeHeaders(ReadOnlySpan<byte> headerBytes)
    {
        var headers = new Dictionary<string, string>(StringComparer.Ordinal);
        var offset = 0;

        while (offset < headerBytes.Length)
        {
            var nameLength = headerBytes[offset];
            offset++;

            var name = Encoding.UTF8.GetString(headerBytes.Slice(offset, nameLength));
            offset += nameLength;

            var valueType = headerBytes[offset];
            offset++;

            var (value, consumed) = DecodeHeaderValue(valueType, headerBytes[offset..]);
            offset += consumed;

            headers[name] = value;
        }

        return headers;
    }

    private static (string Value, int Consumed) DecodeHeaderValue(byte valueType, ReadOnlySpan<byte> remaining) =>
        valueType switch
        {
            0 => ("true", 0),
            1 => ("false", 0),
            2 => (((sbyte) remaining[0]).ToString(System.Globalization.CultureInfo.InvariantCulture), 1),
            3 => (BinaryPrimitives.ReadInt16BigEndian(remaining).ToString(System.Globalization.CultureInfo.InvariantCulture), 2),
            4 => (BinaryPrimitives.ReadInt32BigEndian(remaining).ToString(System.Globalization.CultureInfo.InvariantCulture), 4),
            5 => (BinaryPrimitives.ReadInt64BigEndian(remaining).ToString(System.Globalization.CultureInfo.InvariantCulture), 8),
            6 => DecodeLengthPrefixed(remaining, static bytes => Convert.ToBase64String(bytes)),
            7 => DecodeLengthPrefixed(remaining, static bytes => Encoding.UTF8.GetString(bytes)),
            8 => (BinaryPrimitives.ReadInt64BigEndian(remaining).ToString(System.Globalization.CultureInfo.InvariantCulture), 8),
            9 => (Convert.ToHexStringLower(remaining[..16]), 16),
            _ => throw new AwsEventStreamFormatException($"Unrecognized event stream header value type '{valueType}'."),
        };

    private static (string Value, int Consumed) DecodeLengthPrefixed(ReadOnlySpan<byte> remaining, Func<byte[], string> project)
    {
        var length = BinaryPrimitives.ReadUInt16BigEndian(remaining);
        var bytes = remaining.Slice(2, length).ToArray();
        return (project(bytes), 2 + length);
    }

    private static async Task<int> ReadUpToAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(total), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return total;
    }

    private static async Task ReadExactAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var read = await ReadUpToAsync(stream, buffer, cancellationToken).ConfigureAwait(false);
        if (read < buffer.Length)
        {
            throw new AwsEventStreamFormatException("The stream ended in the middle of a message.");
        }
    }
}
