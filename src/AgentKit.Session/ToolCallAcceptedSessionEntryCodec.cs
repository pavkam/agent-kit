// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session;

using System.Text.Json;

/// <summary>Encodes and decodes the explicit portable v1 accepted tool-call schema.</summary>
/// <remarks>
/// The thread-safe codec reconstructs immutable acceptance evidence only. It never resolves security authority,
/// activates configuration, or mints a grant. Normalization extension data is not part of the schema, so an entry whose
/// record carries any is refused at encode rather than silently truncated.
/// </remarks>
public sealed class ToolCallAcceptedSessionEntryCodec: ISessionEntryCodec
{
    private static readonly SchemaVersion _version = new("1");
    private static readonly SessionEntryCodecLimits _defaultLimits = new(1_048_576, 64, 65_536, 24);
    private static readonly FrozenSet<string> _call = ToolCallSessionEntryJson.Set(
        "callId", "acceptance", "providerAlias", "toolId", "toolVersion", "effects", "externalIdempotencyKey",
        "admission", "normalization", "projectionPolicy", "requestedAt");
    private static readonly FrozenDictionary<string, FrozenSet<string>> _fieldsByPath = CreateSchema();
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ToolCallAcceptedSessionEntryCodec> _logger;
    private readonly SessionEntryCodecLimits _limits;

    /// <summary>Creates a codec with documented default payload, extension, and nesting limits.</summary>
    /// <param name="timeProvider">The required diagnostic clock.</param><param name="logger">The required content-free codec logger.</param>
    /// <exception cref="ArgumentNullException">A parameter is null.</exception>
    public ToolCallAcceptedSessionEntryCodec(TimeProvider timeProvider, ILogger<ToolCallAcceptedSessionEntryCodec> logger)
        : this(timeProvider, logger, _defaultLimits)
    {
    }

    /// <summary>Creates a codec that captures explicit immutable limits in its descriptor and operations.</summary>
    /// <param name="timeProvider">The required diagnostic clock.</param><param name="logger">The required content-free codec logger.</param>
    /// <param name="limits">The validated immutable payload, extension, and nesting limits.</param>
    /// <exception cref="ArgumentNullException">A parameter is null.</exception>
    public ToolCallAcceptedSessionEntryCodec(TimeProvider timeProvider, ILogger<ToolCallAcceptedSessionEntryCodec> logger, SessionEntryCodecLimits limits)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(limits);
        _timeProvider = timeProvider;
        _logger = logger;
        _limits = limits;
        Descriptor = new(new SessionEntryTypeId("agentkit.session/tool-call-accepted"),
            typeof(ToolCallAcceptedSessionEntry), _version, [_version], limits);
    }

    /// <inheritdoc/>
    public SessionEntryCodecDescriptor Descriptor { get; }

    /// <inheritdoc/>
    public SessionEntryEncodeResult Encode(SessionEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var evidence = entry is ToolCallAcceptedSessionEntry accepted && Valid(accepted) ? accepted : null;
        return SessionEntryCodecObservation.Observe(_timeProvider, _logger, "tool-call-accepted.encode", evidence,
            () => EncodeCore(entry));
    }

    /// <inheritdoc/>
    public SessionEntryDecodeResult Decode(SessionEntryWireEnvelope wire)
    {
        ArgumentNullException.ThrowIfNull(wire);
        return SessionEntryCodecObservation.Observe(_timeProvider, _logger, "tool-call-accepted.decode", null,
            () => DecodeCore(wire));
    }

    private SessionEntryEncodeResult EncodeCore(SessionEntry entry)
    {
        Debug.Assert(entry is not null, "The public boundary rejects null entries.");
        if (entry is not ToolCallAcceptedSessionEntry accepted || !Valid(accepted))
        {
            return new SessionEntryEncodeRejected("The entry does not satisfy the accepted tool-call v1 schema.");
        }

        var buffer = new BoundedWriteStream(_limits.MaximumPayloadBytes);
        try
        {
            using var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { MaxDepth = _limits.MaximumJsonDepth });
            Write(writer, accepted);
        }
        catch (InvalidOperationException)
        {
            return new SessionEntryEncodeRejected(
                "The encoded session-entry payload exceeds its configured byte or JSON depth limit.");
        }

        return new SessionEntryEncoded(new SessionEntryWireEnvelope(Descriptor.TypeId, _version, [.. buffer.WrittenSpan]));
    }

    private SessionEntryDecodeResult DecodeCore(SessionEntryWireEnvelope wire)
    {
        Debug.Assert(wire is not null, "The public boundary rejects null envelopes.");
        var rejection = string.Empty;
        if (wire.TypeId != Descriptor.TypeId || wire.SchemaVersion != _version
            || wire.Payload.Length > _limits.MaximumPayloadBytes
            || !PortableSessionEntryJson.TryParse(wire, _limits, _fieldsByPath, out var document, out rejection))
        {
            return new SessionEntryDecodeRejected(rejection.Length == 0
                ? "The wire identity, schema, or payload limit is invalid."
                : rejection);
        }

        using (document)
        {
            try
            {
                return TryDecode(document!.RootElement, out var entry)
                    ? new SessionEntryDecoded(new DecodedSessionEntry(entry, wire))
                    : new SessionEntryDecodeRejected("The accepted tool-call payload is malformed or inconsistent.");
            }
            catch (Exception exception) when (exception is ArgumentException or OverflowException)
            {
                return new SessionEntryDecodeRejected("The accepted tool-call payload violates semantic constraints.");
            }
        }
    }

    private bool TryDecode(JsonElement root, out ToolCallAcceptedSessionEntry entry)
    {
        entry = null!;
        var count = 0;
        var bytes = 0;
        if (!ToolCallSessionEntryJson.TryReadEnvelope(root, _limits, ref count, ref bytes, out var envelope)
            || !PortableSessionEntryJson.ValidateObject(envelope.Call, _call, _limits, ref count, ref bytes)
            || !PortableSessionEntryJson.TryGuid(envelope.Call, "callId", out var callId)
            || !ToolCallSessionEntryJson.TryAcceptance(envelope.Call, _limits, ref count, ref bytes, out var acceptance)
            || acceptance is null
            || !PortableSessionEntryJson.TryString(envelope.Call, "providerAlias", out var alias)
            || !PortableSessionEntryJson.TryString(envelope.Call, "toolId", out var toolId)
            || !PortableSessionEntryJson.TryString(envelope.Call, "toolVersion", out var toolVersion)
            || !ToolCallSessionEntryJson.TryEffects(envelope.Call, _limits, ref count, ref bytes, out var effects)
            || effects is null
            || !ToolCallSessionEntryJson.TryNullableString(envelope.Call, "externalIdempotencyKey", out var key)
            || !ToolCallSessionEntryJson.TryAdmission(envelope.Call, _limits, ref count, ref bytes, out var admission)
            || !ToolCallSessionEntryJson.TryNormalization(envelope.Call, _limits, ref count, ref bytes, out var normalization)
            || !ToolCallSessionEntryJson.TryPolicy(envelope.Call, "projectionPolicy", _limits, ref count, ref bytes, out var projectionKey, out var projectionVersion)
            || !PortableSessionEntryJson.TryTimestamp(envelope.Call, "requestedAt", out var requestedAt))
        {
            return false;
        }

        var call = new AcceptedToolCall(
            envelope.Address.AgentId, envelope.Address.SessionId, envelope.Correlation.RunId, envelope.Correlation.TurnId!.Value,
            envelope.Correlation.OperationId, new ToolCallId(callId), envelope.Authorization, acceptance, new ToolAlias(alias),
            new ToolId(toolId), new ToolVersion(toolVersion), effects, key is null ? null : new IdempotencyKey(key), admission,
            normalization,
            new ToolResultProjectionPolicyReference(new ToolResultProjectionPolicyKey(projectionKey), new ToolResultProjectionPolicyVersion(projectionVersion)),
            requestedAt);
        entry = new ToolCallAcceptedSessionEntry(
            envelope.Id, envelope.Address, envelope.Correlation, envelope.BranchId, envelope.Sequence, envelope.CausalParentId,
            envelope.RecordedAt, _version, call);
        return true;
    }

    private void Write(Utf8JsonWriter writer, ToolCallAcceptedSessionEntry entry)
    {
        Debug.Assert(writer is not null && Valid(entry), "The entry is completely validated before serialization.");
        var call = entry.Call;
        ToolCallSessionEntryJson.WriteStart(writer, entry, (InRunOperationCorrelation) entry.Correlation, call.Authorization);
        PortableSessionEntryJson.WriteGuid(writer, "callId", call.CallId.Value);
        ToolCallSessionEntryJson.WriteAcceptance(writer, call.Acceptance);
        writer.WriteString("providerAlias", call.ProviderAlias.Value);
        writer.WriteString("toolId", call.ToolId.Value);
        writer.WriteString("toolVersion", call.ToolVersion.Value);
        ToolCallSessionEntryJson.WriteEffects(writer, call.Effects);
        ToolCallSessionEntryJson.WriteNullableString(writer, "externalIdempotencyKey", call.ExternalIdempotencyKey?.Value);
        ToolCallSessionEntryJson.WriteAdmission(writer, call.Admission);
        ToolCallSessionEntryJson.WriteNormalization(writer, call.Normalization);
        ToolCallSessionEntryJson.WritePolicy(writer, "projectionPolicy", call.ProjectionPolicy.Key.Value, call.ProjectionPolicy.Version.Value);
        ToolCallSessionEntryJson.WriteTimestamp(writer, "requestedAt", call.RequestedAt);
        ToolCallSessionEntryJson.WriteEnd(writer);
    }

    private bool Valid(ToolCallAcceptedSessionEntry entry)
    {
        var call = entry.Call;
        return call is not null
            && ToolCallSessionEntryJson.ValidEnvelope(entry, _version, call.Authorization, _limits)
            && call.Normalization.Extensions.Values.IsEmpty
            && ToolCallSessionEntryJson.ValidTexts(
                _limits,
                call.ProviderAlias.Value,
                call.ToolId.Value,
                call.ToolVersion.Value,
                call.ExternalIdempotencyKey?.Value,
                call.Admission.CatalogVersion.Value,
                call.Admission.RawArgumentsFingerprint.Value,
                call.Acceptance.ValidatedArgumentsFingerprint.Value,
                call.Normalization.RejectionPolicy.Key.Value,
                call.Normalization.ProjectionPolicy.Key.Value,
                call.Normalization.ExecutionPolicy?.Key.Value,
                call.ProjectionPolicy.Key.Value);
    }

    private static FrozenDictionary<string, FrozenSet<string>> CreateSchema()
    {
        var schema = new Dictionary<string, FrozenSet<string>>(StringComparer.Ordinal);
        ToolCallSessionEntryJson.AddSchema(schema, _call);
        return schema.ToFrozenDictionary(StringComparer.Ordinal);
    }
}
