// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session;

using System.Text.Json;

/// <summary>Encodes and decodes the bounded portable version-one compaction-entry schema.</summary>
public sealed class CompactionSessionEntryCodec: ISessionEntryCodec
{
    private static readonly SchemaVersion _version = new("1");
    private static readonly SessionEntryCodecLimits _limits = new(1_048_576, 64, 65_536, 16);
    private readonly JsonSerializerOptions _json = CreateOptions();

    /// <summary>Creates the immutable compaction-entry codec.</summary>
    public CompactionSessionEntryCodec() => Descriptor = new(
        new SessionEntryTypeId("agentkit.session/compaction"), typeof(CompactionSessionEntry),
        _version, [_version], _limits);

    /// <inheritdoc/>
    public SessionEntryCodecDescriptor Descriptor { get; }

    /// <inheritdoc/>
    public SessionEntryEncodeResult Encode(SessionEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (entry is not CompactionSessionEntry compaction)
        {
            return new SessionEntryEncodeRejected("The entry is not a compaction session entry.");
        }

        try
        {
            var payload = JsonSerializer.SerializeToUtf8Bytes(compaction, _json);
            return payload.Length <= _limits.MaximumPayloadBytes
                ? new SessionEntryEncoded(new SessionEntryWireEnvelope(Descriptor.TypeId, _version, [.. payload]))
                : new SessionEntryEncodeRejected("The encoded compaction entry exceeds its configured byte limit.");
        }
        catch (JsonException)
        {
            return new SessionEntryEncodeRejected("The compaction entry cannot be represented by the version-one schema.");
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return new SessionEntryEncodeRejected("The compaction entry carries a value that cannot be serialized.");
        }
    }

    /// <inheritdoc/>
    public SessionEntryDecodeResult Decode(SessionEntryWireEnvelope wire)
    {
        ArgumentNullException.ThrowIfNull(wire);
        if (wire.TypeId != Descriptor.TypeId || wire.SchemaVersion != _version
            || wire.Payload.Length > _limits.MaximumPayloadBytes)
        {
            return new SessionEntryOpaque(wire);
        }

        try
        {
            var entry = JsonSerializer.Deserialize<CompactionSessionEntry>(wire.Payload.AsSpan(), _json);
            return entry is null
                ? new SessionEntryDecodeRejected("The compaction entry payload is null.")
                : new SessionEntryDecoded(new DecodedSessionEntry(entry, wire));
        }
        catch (JsonException)
        {
            return new SessionEntryDecodeRejected("The compaction entry payload is malformed.");
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return new SessionEntryDecodeRejected("The compaction entry payload violates its invariants.");
        }
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            TypeInfoResolver = PortableSessionJsonPolymorphism.CreateResolver(),
            MaxDepth = _limits.MaximumJsonDepth,
        };
        options.Converters.Add(new PortableValueObjectJsonConverterFactory());
        return options;
    }
}
