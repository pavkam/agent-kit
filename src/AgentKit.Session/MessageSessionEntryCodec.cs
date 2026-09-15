// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session;

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

/// <summary>Encodes and decodes the bounded portable version-one message-entry schema.</summary>
public sealed class MessageSessionEntryCodec: ISessionEntryCodec
{
    private static readonly SchemaVersion _version = new("1");
    private static readonly SessionEntryCodecLimits _limits = new(1_048_576, 64, 65_536, 16);
    private readonly JsonSerializerOptions _json = CreateOptions();

    /// <summary>Creates the immutable message-entry codec.</summary>
    public MessageSessionEntryCodec() => Descriptor = new(
        new SessionEntryTypeId("agentkit.session/message"), typeof(MessageSessionEntry),
        _version, [_version], _limits);

    /// <inheritdoc/>
    public SessionEntryCodecDescriptor Descriptor { get; }

    /// <inheritdoc/>
    public SessionEntryEncodeResult Encode(SessionEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (entry is not MessageSessionEntry message)
        {
            return new SessionEntryEncodeRejected("The entry is not a message session entry.");
        }
        if (message.SchemaVersion != _version)
        {
            return new SessionEntryEncodeRejected("The message entry schema is not supported by this codec.");
        }

        try
        {
            var payload = JsonSerializer.SerializeToUtf8Bytes(message, _json);
            return payload.Length <= _limits.MaximumPayloadBytes
                ? new SessionEntryEncoded(new SessionEntryWireEnvelope(Descriptor.TypeId, _version, [.. payload]))
                : new SessionEntryEncodeRejected("The encoded message entry exceeds its configured byte limit.");
        }
        catch (JsonException)
        {
            return new SessionEntryEncodeRejected("The message entry cannot be represented by the version-one schema.");
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
            var entry = JsonSerializer.Deserialize<MessageSessionEntry>(wire.Payload.AsSpan(), _json);
            return entry is null
                ? new SessionEntryDecodeRejected("The message entry payload is null.")
                : entry.SchemaVersion != wire.SchemaVersion
                    ? new SessionEntryDecodeRejected("The message entry schema does not match its wire envelope.")
                : new SessionEntryDecoded(new DecodedSessionEntry(entry, wire));
        }
        catch (JsonException)
        {
            return new SessionEntryDecodeRejected("The message entry payload is malformed.");
        }
        catch (ArgumentException)
        {
            return new SessionEntryDecodeRejected("The message entry payload violates its invariants.");
        }
    }

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
