// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Json;

/// <summary>Persists a <see cref="SessionEntry"/> as the shared portable wire envelope produced by the captured codec catalog.</summary>
/// <remarks>
/// <para>
/// This leaf owns no entry format of its own. It delegates to <see cref="ISessionEntryCodecCatalog"/>, the same bounded
/// encoder every other first-party durable adapter uses, so an entry written into a JSON session log reads back through the
/// SQLite adapter and vice versa. The envelope's type identifier, schema version, and opaque payload are the only bytes
/// this converter writes.
/// </para>
/// <para>
/// A missing codec, a rejected decode, and an opaque entry are all durable-format failures rather than caller errors, so
/// each surfaces as <see cref="JsonException"/> and the owning adapter reports unsupported or corrupt evidence.
/// </para>
/// </remarks>
internal sealed class JsonSessionEntryConverter: JsonConverter<SessionEntry>
{
    private const string _typeIdProperty = "typeId";
    private const string _schemaVersionProperty = "schemaVersion";
    private const string _payloadProperty = "payload";
    private readonly ISessionEntryCodecCatalog _codecs;

    /// <summary>Captures the immutable codec catalog used for every encode and decode.</summary>
    /// <param name="codecs">The non-null catalog resolved once at composition.</param>
    /// <exception cref="ArgumentNullException"><paramref name="codecs"/> is null.</exception>
    internal JsonSessionEntryConverter(ISessionEntryCodecCatalog codecs)
    {
        ArgumentNullException.ThrowIfNull(codecs);
        _codecs = codecs;
    }

    /// <inheritdoc/>
    /// <exception cref="JsonException">The envelope is malformed, its payload is not valid base-64, or no codec accepts it.</exception>
    public override SessionEntry Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(typeToConvert);
        ArgumentNullException.ThrowIfNull(options);
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object
            || Text(root, _typeIdProperty, options) is not { } typeId
            || Text(root, _schemaVersionProperty, options) is not { } schemaVersion
            || Text(root, _payloadProperty, options) is not { } payload)
        {
            throw new JsonException("A persisted session entry envelope is incomplete.");
        }

        byte[] decoded;
        try
        {
            decoded = Convert.FromBase64String(payload);
        }
        catch (FormatException exception)
        {
            throw new JsonException("A persisted session entry payload is not valid base-64.", exception);
        }

        var wire = new SessionEntryWireEnvelope(
            new SessionEntryTypeId(typeId), new SchemaVersion(schemaVersion), [.. decoded]);
        return _codecs.Decode(wire) switch
        {
            SessionEntryDecoded value => value.Decoded.Entry,
            SessionEntryDecodeRejected rejected => throw new JsonException(rejected.Reason),
            SessionEntryOpaque => throw new JsonException("The persisted session entry codec is unavailable."),
            _ => throw new JsonException("The persisted session entry decode outcome is unsupported."),
        };
    }

    /// <inheritdoc/>
    /// <exception cref="JsonException">The entry has no durable codec or exceeds its codec's bounds.</exception>
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
        writer.WriteString(Name(_typeIdProperty, options), encoded.Wire.TypeId.Value);
        writer.WriteString(Name(_schemaVersionProperty, options), encoded.Wire.SchemaVersion.Value);
        writer.WriteString(Name(_payloadProperty, options), Convert.ToBase64String(encoded.Wire.Payload.AsSpan()));
        writer.WriteEndObject();
    }

    private static string Name(string property, JsonSerializerOptions options)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(property), "A fixed envelope property name is required.");
        Debug.Assert(options is not null, "The serializer always supplies its effective options.");
        return options.PropertyNamingPolicy?.ConvertName(property) ?? property;
    }

    private static string? Text(JsonElement root, string property, JsonSerializerOptions options)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(property), "A fixed envelope property name is required.");
        return root.TryGetProperty(Name(property, options), out var element) && element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;
    }
}
