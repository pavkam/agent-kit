// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Output;

using System.Runtime.InteropServices;
using System.Text.Encodings.Web;

/// <summary>Serializes already-materialized JSON through a bounded sink without materializing an unbounded decoded token.</summary>
/// <remarks>This helper owns compact JSON and <see cref="JavaScriptEncoder.Default"/>, the fixed first-party fingerprint profile.</remarks>
internal static class BoundedJsonSerializer
{
    private const int _maximumSupportedDepth = 128;

    /// <summary>Computes the canonical hash only when the complete JSON representation fits the captured limit.</summary>
    /// <param name="value">The materialized JSON value to serialize.</param>
    /// <param name="maximumBytes">The positive canonical UTF-8 byte limit.</param>
    /// <param name="cancellationToken">The token observed before processing every JSON value and member.</param>
    /// <param name="hash">The canonical SHA-256 identity on success; otherwise, the default value.</param>
    /// <returns><see langword="true"/> when the value fits within the byte and fixed 128-level depth limits; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maximumBytes"/> is not positive.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is undefined.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public static bool TryComputeHash(JsonElement value, int maximumBytes, CancellationToken cancellationToken, out ContentHash hash)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(value.ValueKind, JsonValueKind.Undefined, nameof(value));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);
        cancellationToken.ThrowIfCancellationRequested();
        hash = default;
        using var stream = new BoundedHashStream(maximumBytes, cancellationToken);
        try
        {
            using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
            {
                Encoder = JavaScriptEncoder.Default,
                Indented = false,
                SkipValidation = false,
            });
            if (!TryWrite(value, writer, stream, maximumBytes, 1, cancellationToken))
            {
                return false;
            }
        }
        catch (OutputSchemaSizeLimitException)
        {
            return false;
        }

        hash = stream.CompleteHash();
        return true;
    }

    /// <summary>Writes one structurally pre-measured JSON value and flushes each token to the bounded sink.</summary>
    /// <param name="value">The JSON value to serialize.</param>
    /// <param name="writer">The fixed-profile JSON writer.</param>
    /// <param name="stream">The bounded sink receiving flushed bytes.</param>
    /// <param name="maximumBytes">The positive byte limit owned by <paramref name="stream"/>.</param>
    /// <param name="depth">The one-based nesting depth of <paramref name="value"/>.</param>
    /// <param name="cancellationToken">The token observed before processing each value and member.</param>
    /// <returns><see langword="true"/> when the value fits; otherwise, <see langword="false"/>.</returns>
    /// <remarks>The fixed depth guard keeps the recursive serializer safe in Release builds if normal structural preflight is bypassed.</remarks>
    private static bool TryWrite(JsonElement value, Utf8JsonWriter writer, BoundedHashStream stream, int maximumBytes, int depth, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Debug.Assert(maximumBytes > 0, "The public serializer entry point validated the byte limit.");
        Debug.Assert(depth > 0, "Recursive serialization always uses a one-based depth.");
        if (depth > _maximumSupportedDepth)
        {
            return false;
        }

        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                writer.Flush();
                foreach (var property in value.EnumerateObject())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!CouldStringFit(JsonMarshal.GetRawUtf8PropertyName(property), stream.Length, maximumBytes))
                    {
                        return false;
                    }

                    writer.WritePropertyName(property.Name);
                    writer.Flush();
                    if (!TryWrite(property.Value, writer, stream, maximumBytes, depth + 1, cancellationToken))
                    {
                        return false;
                    }
                }
                writer.WriteEndObject();
                writer.Flush();
                return true;

            case JsonValueKind.Array:
                writer.WriteStartArray();
                writer.Flush();
                foreach (var item in value.EnumerateArray())
                {
                    if (!TryWrite(item, writer, stream, maximumBytes, depth + 1, cancellationToken))
                    {
                        return false;
                    }
                }
                writer.WriteEndArray();
                writer.Flush();
                return true;

            case JsonValueKind.String:
                if (!CouldStringFit(JsonMarshal.GetRawUtf8Value(value), stream.Length, maximumBytes))
                {
                    return false;
                }
                writer.WriteStringValue(value.GetString());
                writer.Flush();
                return true;

            case JsonValueKind.Number:
                var rawNumber = JsonMarshal.GetRawUtf8Value(value);
                if (!CouldRawValueFit(rawNumber, stream.Length, maximumBytes))
                {
                    return false;
                }
                writer.WriteRawValue(rawNumber, skipInputValidation: true);
                writer.Flush();
                return true;

            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                writer.Flush();
                return true;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                writer.Flush();
                return true;
            case JsonValueKind.Null:
                writer.WriteNullValue();
                writer.Flush();
                return true;
            case JsonValueKind.Undefined:
                throw new InvalidOperationException($"JSON value kind '{value.ValueKind}' cannot be serialized.");
            default:
                throw new UnreachableException($"Unknown JSON value kind '{value.ValueKind}'.");
        }
    }

    /// <summary>Checks whether a raw JSON string spelling can decode within the remaining canonical byte budget.</summary>
    /// <param name="rawUtf8">The raw UTF-8 bytes of a JSON string or property name.</param>
    /// <param name="bytesWritten">The canonical bytes already flushed.</param>
    /// <param name="maximumBytes">The positive total canonical byte limit.</param>
    /// <returns><see langword="true"/> when decoding is bounded; otherwise, <see langword="false"/>.</returns>
    /// <remarks>A raw spelling consumes at most six bytes per canonical UTF-8 byte because <c>\uXXXX</c> is the largest escaped UTF-16-code-unit form.</remarks>
    private static bool CouldStringFit(ReadOnlySpan<byte> rawUtf8, long bytesWritten, int maximumBytes)
    {
        Debug.Assert(bytesWritten >= 0 && bytesWritten <= maximumBytes, "The bounded sink reports bytes within its captured limit.");
        return rawUtf8.Length <= 6L * (maximumBytes - bytesWritten);
    }

    /// <summary>Checks whether a raw number token fits in the remaining canonical byte budget.</summary>
    /// <param name="rawUtf8">The raw UTF-8 bytes of a number token.</param>
    /// <param name="bytesWritten">The canonical bytes already flushed.</param>
    /// <param name="maximumBytes">The positive total canonical byte limit.</param>
    /// <returns><see langword="true"/> when the raw number fits; otherwise, <see langword="false"/>.</returns>
    /// <remarks>Numbers retain their raw spelling under the fixed writer profile, making their raw byte count exact.</remarks>
    private static bool CouldRawValueFit(ReadOnlySpan<byte> rawUtf8, long bytesWritten, int maximumBytes)
    {
        Debug.Assert(bytesWritten >= 0 && bytesWritten <= maximumBytes, "The bounded sink reports bytes within its captured limit.");
        return rawUtf8.Length <= maximumBytes - bytesWritten;
    }
}
