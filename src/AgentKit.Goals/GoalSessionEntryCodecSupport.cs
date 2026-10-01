// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>Holds the bounded JSON encoding both goal session-entry codecs share.</summary>
/// <remarks>The contract is camel-cased, string-enumerated, strict about unknown members, and depth-bounded, so two encodings of the same entry are byte-identical and a payload written by another schema is refused rather than guessed at.</remarks>
internal static class GoalSessionEntryCodecSupport
{
    /// <summary>Gets the schema version both codecs write and read.</summary>
    internal static SchemaVersion Version { get; } = new("1");

    /// <summary>Gets the payload, extension, and depth bounds both codecs enforce.</summary>
    internal static SessionEntryCodecLimits Limits { get; } = new(1_048_576, 64, 65_536, 32);

    /// <summary>Gets the shared serializer options.</summary>
    internal static JsonSerializerOptions Options { get; } = CreateOptions();

    /// <summary>Encodes an entry through its document.</summary>
    /// <typeparam name="TEntry">The concrete entry type.</typeparam>
    /// <typeparam name="TDocument">The persisted payload type.</typeparam>
    /// <param name="descriptor">The codec descriptor naming the wire type.</param>
    /// <param name="entry">The entry to encode.</param>
    /// <param name="toDocument">Captures the entry's document.</param>
    /// <returns>The encoded wire envelope or a typed rejection.</returns>
    internal static SessionEntryEncodeResult Encode<TEntry, TDocument>(
        SessionEntryCodecDescriptor descriptor, SessionEntry entry, Func<TEntry, TDocument> toDocument)
        where TEntry : SessionEntry
        where TDocument : class
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(toDocument);
        if (entry is not TEntry typed)
        {
            return new SessionEntryEncodeRejected("The entry is not of the type this codec owns.");
        }

        try
        {
            var payload = JsonSerializer.SerializeToUtf8Bytes(toDocument(typed), Options);
            return payload.Length <= Limits.MaximumPayloadBytes
                ? new SessionEntryEncoded(new SessionEntryWireEnvelope(descriptor.TypeId, Version, [.. payload]))
                : new SessionEntryEncodeRejected("The encoded goal entry exceeds its configured byte limit.");
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return new SessionEntryEncodeRejected("The goal entry cannot be represented by the version-one schema.");
        }
    }

    /// <summary>Decodes a wire envelope through its document.</summary>
    /// <typeparam name="TDocument">The persisted payload type.</typeparam>
    /// <typeparam name="TEntry">The concrete entry type.</typeparam>
    /// <param name="descriptor">The codec descriptor naming the wire type.</param>
    /// <param name="wire">The wire envelope.</param>
    /// <param name="toEntry">Restores the entry from its document.</param>
    /// <returns>The decoded entry, an opaque passthrough for another type or version, or a typed rejection.</returns>
    internal static SessionEntryDecodeResult Decode<TDocument, TEntry>(
        SessionEntryCodecDescriptor descriptor, SessionEntryWireEnvelope wire, Func<TDocument, TEntry> toEntry)
        where TDocument : class
        where TEntry : SessionEntry
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(wire);
        ArgumentNullException.ThrowIfNull(toEntry);
        if (wire.TypeId != descriptor.TypeId || wire.SchemaVersion != Version || wire.Payload.Length > Limits.MaximumPayloadBytes)
        {
            return new SessionEntryOpaque(wire);
        }

        try
        {
            var document = JsonSerializer.Deserialize<TDocument>(wire.Payload.AsSpan(), Options);
            return document is null
                ? new SessionEntryDecodeRejected("The goal entry payload is null.")
                : new SessionEntryDecoded(new DecodedSessionEntry(toEntry(document), wire));
        }
        catch (JsonException)
        {
            return new SessionEntryDecodeRejected("The goal entry payload is malformed.");
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NotSupportedException or FormatException or InvalidDataException)
        {
            return new SessionEntryDecodeRejected("The goal entry payload violates its invariants.");
        }
    }

    private static JsonSerializerOptions CreateOptions() => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        NumberHandling = JsonNumberHandling.Strict,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = Limits.MaximumJsonDepth,
        Converters = { new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false) },
    };
}
