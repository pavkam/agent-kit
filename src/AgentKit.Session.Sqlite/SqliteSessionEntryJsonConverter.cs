// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Sqlite;

/// <summary>Persists session entries through stable bounded wire envelopes and the captured codec catalog.</summary>
internal sealed class SqliteSessionEntryJsonConverter: JsonConverter<SessionEntry>
{
    private readonly ISessionEntryCodecCatalog _codecs;

    /// <summary>Captures the immutable codec catalog used for encoding and decoding.</summary>
    /// <param name="codecs">The non-null codec catalog.</param>
    internal SqliteSessionEntryJsonConverter(ISessionEntryCodecCatalog codecs)
    {
        ArgumentNullException.ThrowIfNull(codecs);
        _codecs = codecs;
    }

    /// <inheritdoc/>
    public override SessionEntry Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(typeToConvert);
        ArgumentNullException.ThrowIfNull(options);
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        var wire = new SessionEntryWireEnvelope(
            new SessionEntryTypeId(root.GetProperty("typeId").GetString()!),
            new SchemaVersion(root.GetProperty("schemaVersion").GetString()!),
            [.. Convert.FromBase64String(root.GetProperty("payload").GetString()!)]);
        return _codecs.Decode(wire) switch
        {
            SessionEntryDecoded decoded => decoded.Decoded.Entry,
            SessionEntryDecodeRejected rejected => throw new JsonException(rejected.Reason),
            SessionEntryOpaque => throw new JsonException("The persisted session entry codec is unavailable."),
            _ => throw new JsonException("The persisted session entry decode outcome is unsupported."),
        };
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, SessionEntry value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(options);
        if (_codecs.Encode(value) is not SessionEntryEncoded encoded)
        {
            throw new JsonException("The session entry has no durable codec.");
        }

        writer.WriteStartObject();
        writer.WriteString("typeId", encoded.Wire.TypeId.Value);
        writer.WriteString("schemaVersion", encoded.Wire.SchemaVersion.Value);
        writer.WriteString("payload", Convert.ToBase64String(encoded.Wire.Payload.AsSpan()));
        writer.WriteEndObject();
    }

}
