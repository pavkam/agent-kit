// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session;

using System.Text.Json;

/// <summary>Encodes and decodes the bounded portable version-one input-admitted-entry schema.</summary>
public sealed class InputAdmittedSessionEntryCodec: ISessionEntryCodec
{
    private static readonly SchemaVersion _version = new("1");
    private static readonly SessionEntryCodecLimits _limits = new(1_048_576, 64, 65_536, 16);
    private readonly JsonSerializerOptions _json = CreateOptions();

    /// <summary>Creates the immutable input-admitted-entry codec.</summary>
    public InputAdmittedSessionEntryCodec() => Descriptor = new(
        new SessionEntryTypeId("agentkit.session/input-admitted"), typeof(InputAdmittedSessionEntry),
        _version, [_version], _limits);

    /// <inheritdoc/>
    public SessionEntryCodecDescriptor Descriptor { get; }

    /// <inheritdoc/>
    public SessionEntryEncodeResult Encode(SessionEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (entry is not InputAdmittedSessionEntry admitted)
        {
            return new SessionEntryEncodeRejected("The entry is not a input-admitted session entry.");
        }

        try
        {
            var payload = JsonSerializer.SerializeToUtf8Bytes(admitted, _json);
            return payload.Length <= _limits.MaximumPayloadBytes
                ? new SessionEntryEncoded(new SessionEntryWireEnvelope(Descriptor.TypeId, _version, [.. payload]))
                : new SessionEntryEncodeRejected("The encoded input-admitted entry exceeds its configured byte limit.");
        }
        catch (JsonException)
        {
            return new SessionEntryEncodeRejected("The input-admitted entry cannot be represented by the version-one schema.");
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return new SessionEntryEncodeRejected("The input-admitted entry carries a value that cannot be serialized.");
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
            using var document = JsonDocument.Parse(wire.Payload.AsMemory());
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return new SessionEntryDecodeRejected("The input-admitted entry payload is malformed.");
            }

            var entry = new InputAdmittedSessionEntry(
                Required<SessionEntryId>(root, "Id"), Required<SessionAddress>(root, "Address"),
                Required<BeforeRunOperationCorrelation>(root, "Correlation"), Required<BranchId>(root, "BranchId"),
                Required<SessionSequence>(root, "Sequence"), Optional<SessionEntryId>(root, "CausalParentId"),
                Required<DateTimeOffset>(root, "RecordedAt"), Required<SchemaVersion>(root, "SchemaVersion"),
                Required<AdmittedInput>(root, "Input"));
            return new SessionEntryDecoded(new DecodedSessionEntry(entry, wire));
        }
        catch (JsonException)
        {
            return new SessionEntryDecodeRejected("The input-admitted entry payload is malformed.");
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return new SessionEntryDecodeRejected("The input-admitted entry payload violates its invariants.");
        }
    }

    /// <summary>Reads one required field through the codec's bounded serializer policy.</summary>
    /// <typeparam name="T">The field value type.</typeparam>
    /// <param name="element">The containing object.</param>
    /// <param name="name">The exact field name.</param>
    /// <returns>The reconstructed non-null field value.</returns>
    /// <exception cref="JsonException">The field is absent, null, or malformed.</exception>
    private T Required<T>(JsonElement element, string name) =>
        element.TryGetProperty(name, out var field)
            ? field.Deserialize<T>(_json) ?? throw new JsonException($"The required {name} field is null.")
            : throw new JsonException($"The required {name} field is missing.");

    /// <summary>Reads one optional value-type field, treating an absent or null field as no value.</summary>
    /// <typeparam name="T">The nonnullable field value type.</typeparam>
    /// <param name="element">The containing object.</param>
    /// <param name="name">The exact field name.</param>
    /// <returns>The reconstructed value, or <see langword="null"/> when the field is absent or null.</returns>
    /// <exception cref="JsonException">The present field is malformed.</exception>
    private T? Optional<T>(JsonElement element, string name) where T : struct =>
        element.TryGetProperty(name, out var field) ? field.Deserialize<T?>(_json) : null;

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
