// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session;

using System.Text.Json;

/// <summary>Encodes and decodes the explicit portable v1 terminal tool-call evidence schema.</summary>
/// <remarks>
/// <para>
/// The schema is content-free: it carries identity, status, side-effect certainty, retryability, safe error, acceptance
/// and grant correlation, normalization provenance, policy references, and timing. A result that carries content, usage,
/// or extension data is refused at encode, because the schema has no field for them and a silent drop would misstate the
/// terminal record. The thread-safe codec never resolves security authority or mints a grant.
/// </para>
/// <para>The numeric terminal status is retained exactly, including a future value this build does not name.</para>
/// </remarks>
public sealed class ToolCallTerminalSessionEntryCodec: ISessionEntryCodec
{
    private static readonly SchemaVersion _version = new("1");
    private static readonly SessionEntryCodecLimits _defaultLimits = new(1_048_576, 64, 65_536, 24);
    private static readonly FrozenSet<string> _call = ToolCallSessionEntryJson.Set(
        "callId", "grantId", "acceptance", "providerAlias", "toolId", "toolVersion", "effects", "externalIdempotencyKey",
        "admission", "status", "error", "sideEffectCertainty", "retryable", "normalization", "normalizationInfo",
        "projectionPolicy", "requestedAt", "invocationStartedAt", "completedAt");
    private static readonly FrozenDictionary<string, FrozenSet<string>> _fieldsByPath = CreateSchema();
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ToolCallTerminalSessionEntryCodec> _logger;
    private readonly SessionEntryCodecLimits _limits;

    /// <summary>Creates a codec with documented default payload, extension, and nesting limits.</summary>
    /// <param name="timeProvider">The required diagnostic clock.</param><param name="logger">The required content-free codec logger.</param>
    /// <exception cref="ArgumentNullException">A parameter is null.</exception>
    public ToolCallTerminalSessionEntryCodec(TimeProvider timeProvider, ILogger<ToolCallTerminalSessionEntryCodec> logger)
        : this(timeProvider, logger, _defaultLimits)
    {
    }

    /// <summary>Creates a codec that captures explicit immutable limits in its descriptor and operations.</summary>
    /// <param name="timeProvider">The required diagnostic clock.</param><param name="logger">The required content-free codec logger.</param>
    /// <param name="limits">The validated immutable payload, extension, and nesting limits.</param>
    /// <exception cref="ArgumentNullException">A parameter is null.</exception>
    public ToolCallTerminalSessionEntryCodec(TimeProvider timeProvider, ILogger<ToolCallTerminalSessionEntryCodec> logger, SessionEntryCodecLimits limits)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(limits);
        _timeProvider = timeProvider;
        _logger = logger;
        _limits = limits;
        Descriptor = new(new SessionEntryTypeId("agentkit.session/tool-call-terminal"),
            typeof(ToolCallTerminalSessionEntry), _version, [_version], limits);
    }

    /// <inheritdoc/>
    public SessionEntryCodecDescriptor Descriptor { get; }

    /// <inheritdoc/>
    public SessionEntryEncodeResult Encode(SessionEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var evidence = entry is ToolCallTerminalSessionEntry terminal && Valid(terminal) ? terminal : null;
        return SessionEntryCodecObservation.Observe(_timeProvider, _logger, "tool-call-terminal.encode", evidence,
            () => EncodeCore(entry));
    }

    /// <inheritdoc/>
    public SessionEntryDecodeResult Decode(SessionEntryWireEnvelope wire)
    {
        ArgumentNullException.ThrowIfNull(wire);
        return SessionEntryCodecObservation.Observe(_timeProvider, _logger, "tool-call-terminal.decode", null,
            () => DecodeCore(wire));
    }

    private SessionEntryEncodeResult EncodeCore(SessionEntry entry)
    {
        Debug.Assert(entry is not null, "The public boundary rejects null entries.");
        if (entry is not ToolCallTerminalSessionEntry terminal || !Valid(terminal))
        {
            return new SessionEntryEncodeRejected("The entry does not satisfy the terminal tool-call v1 schema.");
        }

        var buffer = new BoundedWriteStream(_limits.MaximumPayloadBytes);
        try
        {
            using var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { MaxDepth = _limits.MaximumJsonDepth });
            Write(writer, terminal);
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
                    : new SessionEntryDecodeRejected("The terminal tool-call payload is malformed or inconsistent.");
            }
            catch (Exception exception) when (exception is ArgumentException or OverflowException)
            {
                return new SessionEntryDecodeRejected("The terminal tool-call payload violates semantic constraints.");
            }
        }
    }

    private bool TryDecode(JsonElement root, out ToolCallTerminalSessionEntry entry)
    {
        entry = null!;
        var count = 0;
        var bytes = 0;
        if (!ToolCallSessionEntryJson.TryReadEnvelope(root, _limits, ref count, ref bytes, out var envelope)
            || !PortableSessionEntryJson.ValidateObject(envelope.Call, _call, _limits, ref count, ref bytes)
            || !PortableSessionEntryJson.TryGuid(envelope.Call, "callId", out var callId)
            || !PortableSessionEntryJson.TryOptionalGuid(envelope.Call, "grantId", out var grantId)
            || !ToolCallSessionEntryJson.TryAcceptance(envelope.Call, _limits, ref count, ref bytes, out var acceptance)
            || !PortableSessionEntryJson.TryString(envelope.Call, "providerAlias", out var alias)
            || !ToolCallSessionEntryJson.TryNullableString(envelope.Call, "toolId", out var toolId)
            || !ToolCallSessionEntryJson.TryNullableString(envelope.Call, "toolVersion", out var toolVersion)
            || !ToolCallSessionEntryJson.TryEffects(envelope.Call, _limits, ref count, ref bytes, out var effects)
            || !ToolCallSessionEntryJson.TryNullableString(envelope.Call, "externalIdempotencyKey", out var key)
            || !ToolCallSessionEntryJson.TryAdmission(envelope.Call, _limits, ref count, ref bytes, out var admission)
            || !PortableSessionEntryJson.TryInt64(envelope.Call, "status", out var status) || status is < int.MinValue or > int.MaxValue
            || !ToolCallSessionEntryJson.TryError(envelope.Call, _limits, ref count, ref bytes, out var error)
            || !PortableSessionEntryJson.TryInt64(envelope.Call, "sideEffectCertainty", out var certainty)
            || !Enum.IsDefined((SideEffectCertainty) certainty)
            || !ToolCallSessionEntryJson.TryBool(envelope.Call, "retryable", out var retryable)
            || !ToolCallSessionEntryJson.TryNormalization(envelope.Call, _limits, ref count, ref bytes, out var normalization)
            || !ToolCallSessionEntryJson.TryNormalizationInfo(envelope.Call, _limits, ref count, ref bytes, out var info)
            || !ToolCallSessionEntryJson.TryPolicy(envelope.Call, "projectionPolicy", _limits, ref count, ref bytes, out var projectionKey, out var projectionVersion)
            || !PortableSessionEntryJson.TryTimestamp(envelope.Call, "requestedAt", out var requestedAt)
            || !ToolCallSessionEntryJson.TryNullableTimestamp(envelope.Call, "invocationStartedAt", out var invocationStartedAt)
            || !PortableSessionEntryJson.TryTimestamp(envelope.Call, "completedAt", out var completedAt))
        {
            return false;
        }

        var result = new ToolCallResult(
            envelope.Address.AgentId, envelope.Address.SessionId, envelope.Correlation.RunId, envelope.Correlation.TurnId!.Value,
            envelope.Correlation.OperationId, new ToolCallId(callId), envelope.Authorization,
            grantId is { } grant ? new GrantId(grant) : null, acceptance, new ToolAlias(alias),
            toolId is null ? null : new ToolId(toolId), toolVersion is null ? null : new ToolVersion(toolVersion), effects,
            key is null ? null : new IdempotencyKey(key), admission, (ToolTerminalStatus) status, [], error,
            (SideEffectCertainty) certainty, usage: null, retryable, normalization, info,
            new ToolResultProjectionPolicyReference(new ToolResultProjectionPolicyKey(projectionKey), new ToolResultProjectionPolicyVersion(projectionVersion)),
            requestedAt, invocationStartedAt, completedAt, ExtensionData.Empty);
        entry = new ToolCallTerminalSessionEntry(
            envelope.Id, envelope.Address, envelope.Correlation, envelope.BranchId, envelope.Sequence, envelope.CausalParentId,
            envelope.RecordedAt, _version, result);
        return true;
    }

    private void Write(Utf8JsonWriter writer, ToolCallTerminalSessionEntry entry)
    {
        Debug.Assert(writer is not null && Valid(entry), "The entry is completely validated before serialization.");
        var result = entry.Result;
        ToolCallSessionEntryJson.WriteStart(writer, entry, (InRunOperationCorrelation) entry.Correlation, result.Authorization);
        PortableSessionEntryJson.WriteGuid(writer, "callId", result.CallId.Value);
        PortableSessionEntryJson.WriteOptionalGuid(writer, "grantId", result.GrantId?.Value);
        ToolCallSessionEntryJson.WriteAcceptance(writer, result.Acceptance);
        writer.WriteString("providerAlias", result.ProviderAlias.Value);
        ToolCallSessionEntryJson.WriteNullableString(writer, "toolId", result.ToolId?.Value);
        ToolCallSessionEntryJson.WriteNullableString(writer, "toolVersion", result.ToolVersion?.Value);
        ToolCallSessionEntryJson.WriteEffects(writer, result.Effects);
        ToolCallSessionEntryJson.WriteNullableString(writer, "externalIdempotencyKey", result.ExternalIdempotencyKey?.Value);
        ToolCallSessionEntryJson.WriteAdmission(writer, result.Admission);
        writer.WriteNumber("status", (int) result.Status);
        ToolCallSessionEntryJson.WriteError(writer, result.Error);
        writer.WriteNumber("sideEffectCertainty", (int) result.SideEffectCertainty);
        writer.WriteBoolean("retryable", result.Retryable);
        ToolCallSessionEntryJson.WriteNormalization(writer, result.Normalization);
        ToolCallSessionEntryJson.WriteNormalizationInfo(writer, result.NormalizationInfo);
        ToolCallSessionEntryJson.WritePolicy(writer, "projectionPolicy", result.ProjectionPolicy.Key.Value, result.ProjectionPolicy.Version.Value);
        ToolCallSessionEntryJson.WriteTimestamp(writer, "requestedAt", result.RequestedAt);
        ToolCallSessionEntryJson.WriteNullableTimestamp(writer, "invocationStartedAt", result.InvocationStartedAt);
        ToolCallSessionEntryJson.WriteTimestamp(writer, "completedAt", result.CompletedAt);
        ToolCallSessionEntryJson.WriteEnd(writer);
    }

    private bool Valid(ToolCallTerminalSessionEntry entry)
    {
        var result = entry.Result;
        return result is not null
            && ToolCallSessionEntryJson.ValidEnvelope(entry, _version, result.Authorization, _limits)
            && result.Content.IsEmpty
            && result.Usage is null
            && result.Extensions.Values.IsEmpty
            && result.Normalization.Extensions.Values.IsEmpty
            && result.NormalizationInfo.Extensions.Values.IsEmpty
            && (result.Error is null || result.Error.Extensions.Values.IsEmpty)
            && ToolCallSessionEntryJson.ValidTexts(
                _limits,
                result.ProviderAlias.Value,
                result.ToolId?.Value,
                result.ToolVersion?.Value,
                result.ExternalIdempotencyKey?.Value,
                result.Admission.CatalogVersion.Value,
                result.Admission.RawArgumentsFingerprint.Value,
                result.Acceptance?.ValidatedArgumentsFingerprint.Value,
                result.Error?.SafeMessage,
                result.Error?.ExternalCode,
                result.Normalization.RejectionPolicy.Key.Value,
                result.Normalization.ProjectionPolicy.Key.Value,
                result.Normalization.ExecutionPolicy?.Key.Value,
                result.ProjectionPolicy.Key.Value);
    }

    private static FrozenDictionary<string, FrozenSet<string>> CreateSchema()
    {
        var schema = new Dictionary<string, FrozenSet<string>>(StringComparer.Ordinal);
        ToolCallSessionEntryJson.AddSchema(schema, _call);
        return schema.ToFrozenDictionary(StringComparer.Ordinal);
    }
}
