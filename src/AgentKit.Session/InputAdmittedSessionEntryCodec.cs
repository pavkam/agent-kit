// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session;

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

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
            var entry = new InputAdmittedSessionEntry(
                Required<SessionEntryId>(root, "Id"), Required<SessionAddress>(root, "Address"),
                Required<BeforeRunOperationCorrelation>(root, "Correlation"), Required<BranchId>(root, "BranchId"),
                Required<SessionSequence>(root, "Sequence"), root.GetProperty("CausalParentId").Deserialize<SessionEntryId?>(_json),
                Required<DateTimeOffset>(root, "RecordedAt"), Required<SchemaVersion>(root, "SchemaVersion"),
                Required<AdmittedInput>(root, "Input"));
            return new SessionEntryDecoded(new DecodedSessionEntry(entry, wire));
        }
        catch (JsonException)
        {
            return new SessionEntryDecodeRejected("The input-admitted entry payload is malformed.");
        }
        catch (ArgumentException)
        {
            return new SessionEntryDecodeRejected("The input-admitted entry payload violates its invariants.");
        }
    }

    /// <summary>Reads one required field through the codec's bounded serializer policy.</summary>
    /// <typeparam name="T">The field value type.</typeparam>
    /// <param name="element">The containing object.</param>
    /// <param name="name">The exact field name.</param>
    /// <returns>The reconstructed non-null field value.</returns>
    private T Required<T>(JsonElement element, string name) =>
        element.GetProperty(name).Deserialize<T>(_json)
        ?? throw new JsonException($"The required {name} field is null.");

    private static JsonSerializerOptions CreateOptions()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(static info =>
        {
            if (info.Type == typeof(OperationCorrelation))
            {
                info.PolymorphismOptions = Polymorphism(
                    (typeof(BeforeRunOperationCorrelation), "before-run"),
                    (typeof(InRunOperationCorrelation), "in-run"),
                    (typeof(AfterRunOperationCorrelation), "after-run"));
            }
            else if (info.Type == typeof(AgentMessage))
            {
                info.PolymorphismOptions = Polymorphism(
                    (typeof(SystemMessage), "system"), (typeof(DeveloperMessage), "developer"),
                    (typeof(UserMessage), "user"), (typeof(AssistantMessage), "assistant"),
                    (typeof(ToolMessage), "tool"), (typeof(RuntimeMessage), "runtime"));
            }
            else if (info.Type == typeof(ContentPart))
            {
                info.PolymorphismOptions = Polymorphism(
                    (typeof(TextPart), "text"), (typeof(StructuredDataPart), "structured"),
                    (typeof(ToolCallPart), "tool-call"), (typeof(ToolResultPart), "tool-result"),
                    (typeof(ReasoningPart), "reasoning"), (typeof(MediaReferencePart), "media"),
                    (typeof(UnknownContentPart), "unknown"));
            }
        });
        var options = new JsonSerializerOptions { TypeInfoResolver = resolver, MaxDepth = _limits.MaximumJsonDepth };
        options.Converters.Add(new PortableValueObjectJsonConverterFactory());
        return options;
    }

    private static JsonPolymorphismOptions Polymorphism(params (Type Type, string Name)[] types)
    {
        var result = new JsonPolymorphismOptions { TypeDiscriminatorPropertyName = "$kind" };
        foreach (var (type, name) in types) { result.DerivedTypes.Add(new JsonDerivedType(type, name)); }
        return result;
    }
}
