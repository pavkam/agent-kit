// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session;

using System.Globalization;
using System.Text;
using System.Text.Json;

/// <summary>Provides strict, bounded JSON primitives shared by explicit first-party session-entry codecs.</summary>
/// <remarks>The helpers perform no CLR type activation and preserve text and numeric values without normalization.</remarks>
internal static class PortableSessionEntryJson
{
    private static readonly UTF8Encoding _strictUtf8 = new(false, true);

    /// <summary>Validates bounded UTF-8 JSON before creating the retained parse tree.</summary>
    /// <param name="wire">The validated non-null wire envelope.</param>
    /// <param name="limits">The validated codec limits.</param>
    /// <param name="fieldsByPath">Known property names keyed by their slash-separated object path.</param>
    /// <param name="document">The parsed document on success; otherwise <see langword="null"/>.</param>
    /// <param name="rejection">A content-free rejection description.</param>
    /// <returns><see langword="true"/> only when the payload passes preflight and parsing.</returns>
    /// <exception cref="ArgumentNullException">A required reference is null.</exception>
    internal static bool TryParse(SessionEntryWireEnvelope wire, SessionEntryCodecLimits limits,
        IReadOnlyDictionary<string, FrozenSet<string>> fieldsByPath,
        out JsonDocument? document, out string rejection)
    {
        ArgumentNullException.ThrowIfNull(wire);
        ArgumentNullException.ThrowIfNull(limits);
        ArgumentNullException.ThrowIfNull(fieldsByPath);
        try
        {
            var reader = new Utf8JsonReader(wire.Payload.AsSpan(), new JsonReaderOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = limits.MaximumJsonDepth,
            });
            var unknownCount = 0;
            var unknownBytes = 0;
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject
                || !ScanObject(ref reader, string.Empty, fieldsByPath, limits, ref unknownCount, ref unknownBytes)
                || reader.Read())
            {
                document = null;
                rejection = "The session-entry payload is not a bounded JSON object.";
                return false;
            }

            document = JsonDocument.Parse(wire.Payload.AsMemory(), new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = limits.MaximumJsonDepth,
            });
            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                rejection = string.Empty;
                return true;
            }

            document.Dispose();
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or DecoderFallbackException)
        {
            // Stored bytes are untrusted data; rejection does not expose parser text or content.
        }

        document = null;
        rejection = "The session-entry payload is not a bounded JSON object.";
        return false;
    }

    /// <summary>Returns whether <paramref name="value"/> is well-formed UTF-16 that can be encoded losslessly as UTF-8.</summary>
    /// <param name="value">The non-null text to validate.</param>
    /// <returns><see langword="true"/> when strict UTF-8 encoding accepts the complete value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    internal static bool IsValidText(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        try
        {
            _ = _strictUtf8.GetByteCount(value);
            return true;
        }
        catch (EncoderFallbackException)
        {
            return false;
        }
    }

    /// <summary>Validates one schema object for duplicate properties and bounded compatible extensions.</summary>
    /// <param name="value">The object element to inspect.</param>
    /// <param name="known">The exact property names interpreted by the current schema.</param>
    /// <param name="limits">The immutable codec extension limits.</param>
    /// <param name="unknownCount">The cumulative compatible-field count, updated on success or rejection.</param>
    /// <param name="unknownBytes">The cumulative raw compatible-property subtree bytes, updated on success or rejection.</param>
    /// <returns><see langword="true"/> when the object and cumulative extensions are valid.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="known"/> or <paramref name="limits"/> is null.</exception>
    internal static bool ValidateObject(JsonElement value, IReadOnlySet<string> known,
        SessionEntryCodecLimits limits, ref int unknownCount, ref int unknownBytes)
    {
        ArgumentNullException.ThrowIfNull(known);
        ArgumentNullException.ThrowIfNull(limits);
        if (value.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
        {
            if (!names.Add(property.Name))
            {
                return false;
            }

            if (known.Contains(property.Name))
            {
                continue;
            }

            if (!ValidateUnknown(property, limits, ref unknownCount, ref unknownBytes))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Reads one required JSON string without conversion.</summary>
    /// <param name="parent">The containing object.</param><param name="name">The exact field name.</param>
    /// <param name="value">The retained string on success.</param><returns>Whether the required string exists.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null or blank.</exception>
    internal static bool TryString(JsonElement parent, string name, out string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        value = string.Empty;
        if (!parent.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String
            || property.GetString() is not { } text)
        {
            return false;
        }

        value = text;
        return true;
    }

    /// <summary>Reads one required, exactly representable JSON Int64 value.</summary>
    /// <param name="parent">The containing object.</param><param name="name">The exact field name.</param>
    /// <param name="value">The integer on success.</param><returns>Whether the field is an in-range integer.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null or blank.</exception>
    internal static bool TryInt64(JsonElement parent, string name, out long value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        value = 0;
        return parent.TryGetProperty(name, out var property)
        && property.ValueKind == JsonValueKind.Number
        && property.TryGetInt64(out value);
    }

    /// <summary>Reads one required nested JSON object.</summary>
    /// <param name="parent">The containing object.</param><param name="name">The exact field name.</param>
    /// <param name="value">The nested object on success.</param><returns>Whether the field is an object.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null or blank.</exception>
    internal static bool TryObject(JsonElement parent, string name, out JsonElement value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return parent.TryGetProperty(name, out value) && value.ValueKind == JsonValueKind.Object;
    }

    /// <summary>Reads one nonempty GUID in canonical lowercase D format.</summary>
    /// <param name="parent">The containing object.</param><param name="name">The exact field name.</param>
    /// <param name="value">The GUID on success.</param><returns>Whether the canonical identity is valid.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null or blank.</exception>
    internal static bool TryGuid(JsonElement parent, string name, out Guid value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        value = default;
        return TryString(parent, name, out var text)
            && Guid.TryParseExact(text, "D", out value)
            && value != Guid.Empty
            && string.Equals(text, value.ToString("D"), StringComparison.Ordinal);
    }

    /// <summary>Reads one required field containing either JSON null or a canonical nonempty GUID.</summary>
    /// <param name="parent">The containing object.</param><param name="name">The exact field name.</param>
    /// <param name="value">The optional GUID on success.</param><returns>Whether the required optional field is valid.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null or blank.</exception>
    internal static bool TryOptionalGuid(JsonElement parent, string name, out Guid? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        value = null;
        return parent.TryGetProperty(name, out var property)
            && (property.ValueKind == JsonValueKind.Null
            || (property.ValueKind == JsonValueKind.String
            && Guid.TryParseExact(property.GetString(), "D", out var parsed)
            && parsed != Guid.Empty
            && string.Equals(property.GetString(), parsed.ToString("D"), StringComparison.Ordinal)
            && Assign(parsed, out value)));
    }

    /// <summary>Reads one timestamp in exact invariant round-trip format.</summary>
    /// <param name="parent">The containing object.</param><param name="name">The exact field name.</param>
    /// <param name="value">The timestamp on success.</param><returns>Whether the timestamp is canonical and in range.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null or blank.</exception>
    internal static bool TryTimestamp(JsonElement parent, string name, out DateTimeOffset value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        value = default;
        return TryString(parent, name, out var text)
            && DateTimeOffset.TryParseExact(text, "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out value)
            && string.Equals(text, value.ToString("O", CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    /// <summary>Writes a validated identity in canonical lowercase D format.</summary>
    /// <param name="writer">The active bounded writer.</param><param name="name">The fixed schema field name.</param>
    /// <param name="value">The nonempty validated GUID.</param>
    /// <exception cref="ArgumentNullException"><paramref name="writer"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null or blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is empty.</exception>
    internal static void WriteGuid(Utf8JsonWriter writer, string name, Guid value)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfEqual(value, default);
        writer.WriteString(name, value.ToString("D"));
    }

    /// <summary>Writes a required optional identity as canonical text or JSON null.</summary>
    /// <param name="writer">The active bounded writer.</param><param name="name">The fixed schema field name.</param>
    /// <param name="value">The optional validated GUID.</param>
    /// <exception cref="ArgumentNullException"><paramref name="writer"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null or blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> contains an empty GUID.</exception>
    internal static void WriteOptionalGuid(Utf8JsonWriter writer, string name, Guid? value)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfEqual(value, (Guid?) Guid.Empty);
        if (value is { } present)
        {
            WriteGuid(writer, name, present);
        }
        else
        {
            writer.WriteNull(name);
        }
    }

    private static bool Assign(Guid parsed, out Guid? value) { value = parsed; return true; }

    private static bool ValidateUnknown(JsonProperty property, SessionEntryCodecLimits limits,
        ref int count, ref int bytes)
    {
        count++;
        bytes = checked(bytes + Encoding.UTF8.GetByteCount(property.Name)
            + Encoding.UTF8.GetByteCount(property.Value.GetRawText()));
        return count <= limits.MaximumExtensionCount
            && bytes <= limits.MaximumExtensionBytes
            && ValidateUnknownChildren(property.Value);
    }

    private static bool ValidateUnknownChildren(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray())
            {
                if (!ValidateUnknownChildren(item))
                {
                    return false;
                }
            }

            return true;
        }

        if (value.ValueKind != JsonValueKind.Object)
        {
            return true;
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var child in value.EnumerateObject())
        {
            if (!names.Add(child.Name) || !ValidateUnknownChildren(child.Value))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ScanObject(ref Utf8JsonReader reader, string path,
        IReadOnlyDictionary<string, FrozenSet<string>> fieldsByPath, SessionEntryCodecLimits limits,
        ref int unknownCount, ref int unknownBytes)
    {
        Debug.Assert(reader.TokenType == JsonTokenType.StartObject, "The caller positions the reader at an object.");
        var names = new HashSet<string>(StringComparer.Ordinal);
        _ = fieldsByPath.TryGetValue(path, out var known);
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var propertyStart = reader.TokenStartIndex;
            if (reader.TokenType != JsonTokenType.PropertyName || !IsValidJsonStringToken(ref reader)
                || reader.GetString() is not { } name || !names.Add(name)
                || !reader.Read())
            {
                return false;
            }

            var isUnknownSubtree = path.Length > 0 && path[0] == '\0';
            var isKnown = known?.Contains(name) == true;
            if (!ScanValue(ref reader, isKnown ? Join(path, name) : null, fieldsByPath, limits,
                    ref unknownCount, ref unknownBytes))
            {
                return false;
            }

            if (!isKnown)
            {
                unknownCount++;
                if (!isUnknownSubtree)
                {
                    unknownBytes = checked(unknownBytes + checked((int) (reader.BytesConsumed - propertyStart)));
                }

                if (unknownCount > limits.MaximumExtensionCount || unknownBytes > limits.MaximumExtensionBytes)
                {
                    return false;
                }
            }
        }

        return reader.TokenType == JsonTokenType.EndObject;
    }

    private static bool ScanValue(ref Utf8JsonReader reader, string? path,
        IReadOnlyDictionary<string, FrozenSet<string>> fieldsByPath, SessionEntryCodecLimits limits,
        ref int unknownCount, ref int unknownBytes)
    {
        if (reader.TokenType == JsonTokenType.StartObject)
        {
            return ScanObject(ref reader, path ?? "\0", fieldsByPath, limits, ref unknownCount, ref unknownBytes);
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            return IsValidJsonStringToken(ref reader);
        }

        if (reader.TokenType != JsonTokenType.StartArray)
        {
            return true;
        }

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (!ScanValue(ref reader, path, fieldsByPath, limits, ref unknownCount, ref unknownBytes))
            {
                return false;
            }
        }

        return reader.TokenType == JsonTokenType.EndArray;
    }

    private static string Join(string path, string name) => path.Length == 0 ? name : string.Concat(path, "/", name);

    private static bool IsValidJsonStringToken(ref Utf8JsonReader reader)
    {
        Debug.Assert(reader.TokenType is JsonTokenType.String or JsonTokenType.PropertyName,
            "The scanner calls string validation only for JSON string tokens.");
        if (reader.HasValueSequence)
        {
            return false;
        }

        var raw = reader.ValueSpan;
        try
        {
            _ = _strictUtf8.GetCharCount(raw);
        }
        catch (DecoderFallbackException)
        {
            return false;
        }

        for (var index = 0; index < raw.Length; index++)
        {
            if (raw[index] != (byte) '\\')
            {
                continue;
            }

            index++;
            if (index >= raw.Length || raw[index] != (byte) 'u')
            {
                continue;
            }

            if (!TryHexCodeUnit(raw, index + 1, out var codeUnit))
            {
                return false;
            }

            index += 4;
            if (char.IsLowSurrogate((char) codeUnit))
            {
                return false;
            }

            if (!char.IsHighSurrogate((char) codeUnit))
            {
                continue;
            }

            if (index + 6 >= raw.Length || raw[index + 1] != (byte) '\\' || raw[index + 2] != (byte) 'u'
                || !TryHexCodeUnit(raw, index + 3, out var low) || !char.IsLowSurrogate((char) low))
            {
                return false;
            }

            index += 6;
        }

        return true;
    }

    private static bool TryHexCodeUnit(ReadOnlySpan<byte> raw, int start, out ushort value)
    {
        Debug.Assert(start >= 0, "The caller advances from a JSON Unicode escape marker.");
        value = 0;
        if (start > raw.Length - 4)
        {
            return false;
        }

        for (var index = start; index < start + 4; index++)
        {
            var digit = raw[index] switch
            {
                >= (byte) '0' and <= (byte) '9' => raw[index] - (byte) '0',
                >= (byte) 'A' and <= (byte) 'F' => raw[index] - (byte) 'A' + 10,
                >= (byte) 'a' and <= (byte) 'f' => raw[index] - (byte) 'a' + 10,
                _ => -1,
            };
            if (digit < 0)
            {
                return false;
            }

            value = (ushort) ((value << 4) | digit);
        }

        return true;
    }
}
