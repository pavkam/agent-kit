// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session;

using System.Globalization;
using System.Text.Json;

/// <summary>Encodes and decodes the explicit portable v1 input-promotion schema.</summary>
/// <remarks>This thread-safe codec preserves admission order and reconstructs evidence only; decoded claims confer no execution or security authority.</remarks>
public sealed class InputPromotedSessionEntryCodec: ISessionEntryCodec
{
    private static readonly SchemaVersion _version = new("1");
    private static readonly SessionEntryCodecLimits _defaultLimits = new(1_048_576, 64, 65_536, 16);
    private static readonly FrozenSet<string> _root = new[] { "id", "address", "correlation", "branchId", "sequence", "causalParentId", "recordedAt", "executionLaneId", "initiatingAdmissionId", "cutoff", "admissionIds" }.ToFrozenSet(StringComparer.Ordinal);
    private static readonly FrozenSet<string> _address = new[] { "agentId", "sessionId" }.ToFrozenSet(StringComparer.Ordinal);
    private static readonly FrozenSet<string> _correlation = new[] { "kind", "operationId", "runId", "turnId" }.ToFrozenSet(StringComparer.Ordinal);
    private static readonly FrozenDictionary<string, FrozenSet<string>> _fieldsByPath =
        new Dictionary<string, FrozenSet<string>>(StringComparer.Ordinal)
        {
            [string.Empty] = _root,
            ["address"] = _address,
            ["correlation"] = _correlation,
        }.ToFrozenDictionary(StringComparer.Ordinal);
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<InputPromotedSessionEntryCodec> _logger;
    private readonly SessionEntryCodecLimits _limits;

    /// <summary>Creates a thread-safe codec with explicit timing and content-free diagnostics.</summary>
    /// <param name="timeProvider">The non-null clock used only for best-effort duration observation.</param>
    /// <param name="logger">The non-null structured logger; entry content is never emitted.</param>
    /// <exception cref="ArgumentNullException">A parameter is null.</exception>
    public InputPromotedSessionEntryCodec(TimeProvider timeProvider, ILogger<InputPromotedSessionEntryCodec> logger)
        : this(timeProvider, logger, _defaultLimits)
    {
    }

    /// <summary>Creates a thread-safe codec with explicit immutable payload, extension, and depth limits.</summary>
    /// <param name="timeProvider">The non-null clock used only for best-effort duration observation.</param>
    /// <param name="logger">The non-null structured logger; entry content is never emitted.</param>
    /// <param name="limits">The non-null validated limits captured by this codec and its descriptor.</param>
    /// <exception cref="ArgumentNullException">A parameter is null.</exception>
    public InputPromotedSessionEntryCodec(TimeProvider timeProvider,
        ILogger<InputPromotedSessionEntryCodec> logger, SessionEntryCodecLimits limits)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(limits);
        _timeProvider = timeProvider;
        _logger = logger;
        _limits = limits;
        Descriptor = new(new SessionEntryTypeId("agentkit.session/input-promoted"),
            typeof(InputPromotedSessionEntry), _version, [_version], limits);
    }

    /// <inheritdoc/>
    public SessionEntryCodecDescriptor Descriptor { get; }

    /// <inheritdoc/>
    public SessionEntryEncodeResult Encode(SessionEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var evidence = entry is InputPromotedSessionEntry value && Valid(value) ? value : null;
        return SessionEntryCodecObservation.Observe(_timeProvider, _logger, "input-promoted.encode",
            evidence, () => EncodeCore(entry));
    }

    /// <inheritdoc/>
    public SessionEntryDecodeResult Decode(SessionEntryWireEnvelope wire)
    {
        ArgumentNullException.ThrowIfNull(wire);
        return SessionEntryCodecObservation.Observe(_timeProvider, _logger, "input-promoted.decode",
            null, () => DecodeCore(wire));
    }

    private SessionEntryEncodeResult EncodeCore(SessionEntry entry)
    {
        Debug.Assert(entry is not null, "The public encode boundary rejects null entries.");
        if (entry is not InputPromotedSessionEntry value || !Valid(value))
        {
            return new SessionEntryEncodeRejected("The entry does not satisfy the input-promotion v1 schema.");
        }

        var buffer = new BoundedWriteStream(_limits.MaximumPayloadBytes);
        try
        {
            using var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions
            {
                MaxDepth = _limits.MaximumJsonDepth,
            });
            Write(writer, value);
        }
        catch (InvalidOperationException)
        {
            return new SessionEntryEncodeRejected(
                "The encoded session-entry payload exceeds its configured byte or JSON depth limit.");
        }

        return new SessionEntryEncoded(new SessionEntryWireEnvelope(Descriptor.TypeId, _version, [.. buffer.WrittenSpan]));
    }

    private void Write(Utf8JsonWriter writer, InputPromotedSessionEntry value)
    {
        Debug.Assert(writer is not null, "The caller owns a live bounded JSON writer.");
        Debug.Assert(Valid(value), "The entry passed complete schema validation before serialization.");
        var correlation = (InRunOperationCorrelation) value.Correlation;
        writer.WriteStartObject();
        PortableSessionEntryJson.WriteGuid(writer, "id", value.Id.Value);
        writer.WritePropertyName("address");
        writer.WriteStartObject();
        PortableSessionEntryJson.WriteGuid(writer, "agentId", value.Address.AgentId.Value);
        PortableSessionEntryJson.WriteGuid(writer, "sessionId", value.Address.SessionId.Value);
        writer.WriteEndObject();
        writer.WritePropertyName("correlation");
        writer.WriteStartObject();
        writer.WriteString("kind", "inRun");
        PortableSessionEntryJson.WriteGuid(writer, "operationId", correlation.OperationId.Value);
        PortableSessionEntryJson.WriteGuid(writer, "runId", correlation.RunId.Value);
        PortableSessionEntryJson.WriteOptionalGuid(writer, "turnId", correlation.TurnId?.Value);
        writer.WriteEndObject();
        PortableSessionEntryJson.WriteGuid(writer, "branchId", value.BranchId.Value);
        writer.WriteNumber("sequence", value.Sequence.Value);
        PortableSessionEntryJson.WriteOptionalGuid(writer, "causalParentId", value.CausalParentId?.Value);
        writer.WriteString("recordedAt", value.RecordedAt.ToString("O", CultureInfo.InvariantCulture));
        PortableSessionEntryJson.WriteGuid(writer, "executionLaneId", value.ExecutionLaneId.Value);
        PortableSessionEntryJson.WriteGuid(writer, "initiatingAdmissionId", value.InitiatingAdmissionId.Value);
        writer.WriteNumber("cutoff", value.Cutoff.Value);
        writer.WritePropertyName("admissionIds");
        writer.WriteStartArray();
        foreach (var admission in value.AdmissionIds)
        {
            writer.WriteStringValue(admission.Value.ToString("D"));
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private SessionEntryDecodeResult DecodeCore(SessionEntryWireEnvelope wire)
    {
        Debug.Assert(wire is not null, "The public decode boundary rejects null envelopes.");
        var rejection = string.Empty;
        if (wire.TypeId != Descriptor.TypeId || wire.SchemaVersion != _version || wire.Payload.Length > _limits.MaximumPayloadBytes
            || !PortableSessionEntryJson.TryParse(wire, _limits, _fieldsByPath, out var document, out rejection))
        {
            return new SessionEntryDecodeRejected(rejection.Length == 0 ? "The wire identity, schema, or payload limit is invalid." : rejection);
        }

        using (document)
        {
            try
            {
                var root = document!.RootElement;
                var count = 0;
                var bytes = 0;
                if (!PortableSessionEntryJson.ValidateObject(root, _root, _limits, ref count, ref bytes)
                    || !PortableSessionEntryJson.TryGuid(root, "id", out var id)
                    || !TryAddress(root, ref count, ref bytes, out var address)
                    || !TryCorrelation(root, ref count, ref bytes, out var correlation)
                    || !PortableSessionEntryJson.TryGuid(root, "branchId", out var branch)
                    || !PortableSessionEntryJson.TryInt64(root, "sequence", out var sequence) || sequence <= 0
                    || !PortableSessionEntryJson.TryOptionalGuid(root, "causalParentId", out var parent)
                    || !PortableSessionEntryJson.TryTimestamp(root, "recordedAt", out var recordedAt)
                    || !PortableSessionEntryJson.TryGuid(root, "executionLaneId", out var lane)
                    || !PortableSessionEntryJson.TryGuid(root, "initiatingAdmissionId", out var initiating)
                    || !PortableSessionEntryJson.TryInt64(root, "cutoff", out var cutoff) || cutoff < 0
                    || !TryAdmissions(root, out var admissions))
                {
                    return new SessionEntryDecodeRejected("The input-promotion payload is malformed.");
                }

                var entry = new InputPromotedSessionEntry(new SessionEntryId(id), address, correlation,
                    new BranchId(branch), new SessionSequence(sequence), parent is { } p ? new SessionEntryId(p) : null,
                    recordedAt, _version, new ExecutionLaneId(lane), new AdmissionId(initiating),
                    new SessionSequence(cutoff), admissions);
                return new SessionEntryDecoded(new DecodedSessionEntry(entry, wire));
            }
            catch (Exception exception) when (exception is ArgumentException or OverflowException)
            {
                return new SessionEntryDecodeRejected("The input-promotion payload violates semantic constraints.");
            }
        }
    }

    private bool Valid(InputPromotedSessionEntry value) => value.Id != default
        && value.Address is not null && value.Address.AgentId != default && value.Address.SessionId != default
        && value.Correlation is InRunOperationCorrelation { OperationId: var operation, RunId: var run } correlation && operation != default && run != default
        && (correlation.TurnId is null || correlation.TurnId.Value != default)
        && value.BranchId != default && value.Sequence.Value > 0
        && (value.CausalParentId is null || value.CausalParentId.Value != default) && value.ExecutionLaneId != default
        && value.InitiatingAdmissionId != default && !value.AdmissionIds.IsDefaultOrEmpty
        && value.AdmissionIds.Length <= _limits.MaximumPayloadBytes / 39
        && value.AdmissionIds.All(static id => id != default)
        && value.AdmissionIds.Distinct().Count() == value.AdmissionIds.Length && value.AdmissionIds.Contains(value.InitiatingAdmissionId)
        && value.SchemaVersion == _version;

    private bool TryAddress(JsonElement root, ref int count, ref int bytes, out SessionAddress address)
    {
        address = null!;
        return PortableSessionEntryJson.TryObject(root, "address", out var item)
            && PortableSessionEntryJson.ValidateObject(item, _address, _limits, ref count, ref bytes)
            && PortableSessionEntryJson.TryGuid(item, "agentId", out var agent)
            && PortableSessionEntryJson.TryGuid(item, "sessionId", out var session)
            && Assign(new SessionAddress(new AgentId(agent), new SessionId(session)), out address);
    }

    private bool TryCorrelation(JsonElement root, ref int count, ref int bytes, out InRunOperationCorrelation correlation)
    {
        correlation = null!;
        return PortableSessionEntryJson.TryObject(root, "correlation", out var item)
            && PortableSessionEntryJson.ValidateObject(item, _correlation, _limits, ref count, ref bytes)
            && PortableSessionEntryJson.TryString(item, "kind", out var kind)
            && kind == "inRun"
            && PortableSessionEntryJson.TryGuid(item, "operationId", out var operation)
            && PortableSessionEntryJson.TryGuid(item, "runId", out var run)
            && PortableSessionEntryJson.TryOptionalGuid(item, "turnId", out var turn)
            && Assign(new InRunOperationCorrelation(new OperationId(operation), new RunId(run),
                turn is { } value ? new TurnId(value) : null), out correlation);
    }
    private static bool TryAdmissions(JsonElement root, out ImmutableArray<AdmissionId> admissions)
    {
        admissions = default;
        if (!root.TryGetProperty("admissionIds", out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var builder = ImmutableArray.CreateBuilder<AdmissionId>();
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || !Guid.TryParseExact(item.GetString(), "D", out var id) || id == Guid.Empty || item.GetString() != id.ToString("D"))
            {
                return false;
            }

            builder.Add(new AdmissionId(id));
        }
        admissions = builder.ToImmutable();
        return !admissions.IsDefaultOrEmpty && admissions.Distinct().Count() == admissions.Length;
    }
    private static bool Assign<T>(T source, out T value)
    {
        value = source;
        return true;
    }
}
