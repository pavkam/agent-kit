// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session;

using System.Globalization;
using System.Text.Json;

/// <summary>Encodes and decodes the explicit portable v1 accepted-operation recovery schema.</summary>
/// <remarks>The thread-safe codec reconstructs immutable recovery evidence only. It never resolves security authority, activates configuration, or mints a grant.</remarks>
public sealed class OperationAcceptedSessionEntryCodec: ISessionEntryCodec
{
    private static readonly SchemaVersion _version = new("1");
    private static readonly SessionEntryCodecLimits _defaultLimits = new(1_048_576, 64, 65_536, 24);
    private static readonly FrozenSet<string> _root = Set("id", "address", "correlation", "branchId", "sequence", "causalParentId", "recordedAt", "state");
    private static readonly FrozenSet<string> _address = Set("agentId", "sessionId");
    private static readonly FrozenSet<string> _correlation = Set("kind", "operationId", "runId", "turnId");
    private static readonly FrozenSet<string> _state = Set("address", "executionLaneId", "laneRevision", "correlation", "operationStateRevision", "identity", "authorization", "sessionProfile", "configuration", "previousCursor", "committedCursor", "promotionCutoff", "initiatingAdmissionId", "promotedAdmissionIds", "materializedEntryIds", "materializedMessageIds", "initialTurnId", "acceptedAt", "state");
    private static readonly FrozenSet<string> _profile = Set("key", "version");
    private static readonly FrozenSet<string> _configuration = Set("configurationVersion", "policyVersion", "fingerprint");
    private static readonly FrozenSet<string> _cursor = Set("branchId", "lastEntryId");
    private static readonly FrozenDictionary<string, FrozenSet<string>> _fieldsByPath = CreateSchema();
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<OperationAcceptedSessionEntryCodec> _logger;
    private readonly SessionEntryCodecLimits _limits;

    /// <summary>Creates a codec with documented default payload, extension, and nesting limits.</summary>
    /// <param name="timeProvider">The required diagnostic clock.</param><param name="logger">The required content-free codec logger.</param>
    /// <exception cref="ArgumentNullException">A parameter is null.</exception>
    public OperationAcceptedSessionEntryCodec(TimeProvider timeProvider, ILogger<OperationAcceptedSessionEntryCodec> logger)
        : this(timeProvider, logger, _defaultLimits)
    {
    }

    /// <summary>Creates a codec that captures explicit immutable limits in its descriptor and operations.</summary>
    /// <param name="timeProvider">The required diagnostic clock.</param><param name="logger">The required content-free codec logger.</param>
    /// <param name="limits">The validated immutable payload, extension, and nesting limits.</param>
    /// <exception cref="ArgumentNullException">A parameter is null.</exception>
    public OperationAcceptedSessionEntryCodec(TimeProvider timeProvider,
        ILogger<OperationAcceptedSessionEntryCodec> logger, SessionEntryCodecLimits limits)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(limits);
        _timeProvider = timeProvider;
        _logger = logger;
        _limits = limits;
        Descriptor = new(new SessionEntryTypeId("agentkit.session/operation-accepted"),
            typeof(OperationAcceptedSessionEntry), _version, [_version], limits);
    }

    /// <inheritdoc/>
    public SessionEntryCodecDescriptor Descriptor { get; }

    /// <inheritdoc/>
    public SessionEntryEncodeResult Encode(SessionEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var evidence = entry is OperationAcceptedSessionEntry accepted && Valid(accepted) ? accepted : null;
        return SessionEntryCodecObservation.Observe(_timeProvider, _logger, "operation-accepted.encode", evidence,
            () => EncodeCore(entry));
    }

    /// <inheritdoc/>
    public SessionEntryDecodeResult Decode(SessionEntryWireEnvelope wire)
    {
        ArgumentNullException.ThrowIfNull(wire);
        return SessionEntryCodecObservation.Observe(_timeProvider, _logger, "operation-accepted.decode", null,
            () => DecodeCore(wire));
    }

    private SessionEntryEncodeResult EncodeCore(SessionEntry entry)
    {
        Debug.Assert(entry is not null, "The public boundary rejects null entries.");
        if (entry is not OperationAcceptedSessionEntry accepted || !Valid(accepted))
        {
            return new SessionEntryEncodeRejected("The entry does not satisfy the accepted-operation v1 schema.");
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
                return TryDecode(document!.RootElement, wire, out var entry)
                    ? new SessionEntryDecoded(new DecodedSessionEntry(entry, wire))
                    : new SessionEntryDecodeRejected("The accepted-operation payload is malformed or inconsistent.");
            }
            catch (Exception exception) when (exception is ArgumentException or OverflowException)
            {
                return new SessionEntryDecodeRejected("The accepted-operation payload violates semantic constraints.");
            }
        }
    }

    private bool TryDecode(JsonElement root, SessionEntryWireEnvelope wire, out OperationAcceptedSessionEntry entry)
    {
        Debug.Assert(wire is not null, "The validated envelope is retained on success.");
        entry = null!;
        var count = 0;
        var bytes = 0;
        if (!PortableSessionEntryJson.ValidateObject(root, _root, _limits, ref count, ref bytes)
            || !PortableSessionEntryJson.TryGuid(root, "id", out var id)
            || !TryAddress(root, "address", ref count, ref bytes, out var address)
            || !TryCorrelation(root, "correlation", ref count, ref bytes, out var correlation)
            || !PortableSessionEntryJson.TryGuid(root, "branchId", out var branch)
            || !PortableSessionEntryJson.TryInt64(root, "sequence", out var sequence) || sequence <= 0
            || !PortableSessionEntryJson.TryOptionalGuid(root, "causalParentId", out var parent)
            || !PortableSessionEntryJson.TryTimestamp(root, "recordedAt", out var recordedAt)
            || !PortableSessionEntryJson.TryObject(root, "state", out var state)
            || !PortableSessionEntryJson.ValidateObject(state, _state, _limits, ref count, ref bytes)
            || !TryState(state, new SessionEntryId(id), address, correlation, new BranchId(branch),
                parent is { } parentId ? new SessionEntryId(parentId) : null, recordedAt, ref count, ref bytes, out var acceptedState))
        {
            return false;
        }

        entry = new OperationAcceptedSessionEntry(new SessionEntryId(id), address, correlation, new BranchId(branch),
            new SessionSequence(sequence), parent is { } value ? new SessionEntryId(value) : null,
            recordedAt, _version, acceptedState);
        return true;
    }

    private bool TryState(JsonElement state, SessionEntryId entryId, SessionAddress entryAddress,
        InRunOperationCorrelation entryCorrelation, BranchId entryBranch, SessionEntryId? entryParent,
        DateTimeOffset recordedAt, ref int count, ref int bytes, out SessionAcceptedRunState accepted)
    {
        accepted = null!;
        if (!TryAddress(state, "address", ref count, ref bytes, out var address) || address != entryAddress
            || !PortableSessionEntryJson.TryGuid(state, "executionLaneId", out var lane)
            || !PortableSessionEntryJson.TryInt64(state, "laneRevision", out var laneRevision) || laneRevision <= 0
            || !TryCorrelation(state, "correlation", ref count, ref bytes, out var correlation) || correlation != entryCorrelation
            || !PortableSessionEntryJson.TryInt64(state, "operationStateRevision", out var operationRevision) || operationRevision <= 0
            || !PortableSessionSecurityJson.TryIdentity(state, _limits, ref count, ref bytes, out var identity)
            || !PortableSessionSecurityJson.TryAuthorization(state, identity, address, correlation, _limits,
                ref count, ref bytes, out var authorization)
            || !TryProfile(state, ref count, ref bytes, out var profile)
            || !TryConfiguration(state, ref count, ref bytes, out var configuration)
            || authorization.ConfigurationVersion != configuration.ConfigurationVersion
            || !TryCursor(state, "previousCursor", ref count, ref bytes, out var previousCursor)
            || !TryCursor(state, "committedCursor", ref count, ref bytes, out var committedCursor)
            || committedCursor.BranchId != entryBranch || committedCursor.LastEntryId != entryId
            || !PortableSessionEntryJson.TryInt64(state, "promotionCutoff", out var cutoff) || cutoff < 0
            || !PortableSessionEntryJson.TryGuid(state, "initiatingAdmissionId", out var initiating)
            || !TryGuidArray<AdmissionId>(state, "promotedAdmissionIds", static value => new(value), out var admissions)
            || !TryGuidArray<SessionEntryId>(state, "materializedEntryIds", static value => new(value), out var entryIds)
            || !TryGuidArray<MessageId>(state, "materializedMessageIds", static value => new(value), out var messageIds)
            || !PortableSessionEntryJson.TryGuid(state, "initialTurnId", out var turn)
            || !PortableSessionEntryJson.TryTimestamp(state, "acceptedAt", out var acceptedAt) || acceptedAt != recordedAt
            || !PortableSessionEntryJson.TryString(state, "state", out var stateName) || stateName != "accepted"
            || entryParent != entryIds[^1])
        {
            return false;
        }

        accepted = new SessionAcceptedRunState(address, new ExecutionLaneId(lane), new SessionLaneRevision(laneRevision),
            correlation, new OperationStateRevision(operationRevision), identity, authorization, profile, configuration,
            previousCursor, committedCursor, new SessionSequence(cutoff), new AdmissionId(initiating), admissions,
            entryIds, messageIds, new TurnId(turn), acceptedAt);
        return true;
    }

    private void Write(Utf8JsonWriter writer, OperationAcceptedSessionEntry entry)
    {
        Debug.Assert(writer is not null && Valid(entry), "The entry is completely validated before serialization.");
        writer.WriteStartObject();
        PortableSessionEntryJson.WriteGuid(writer, "id", entry.Id.Value);
        WriteAddress(writer, "address", entry.Address);
        WriteCorrelation(writer, "correlation", (InRunOperationCorrelation) entry.Correlation);
        PortableSessionEntryJson.WriteGuid(writer, "branchId", entry.BranchId.Value);
        writer.WriteNumber("sequence", entry.Sequence.Value);
        PortableSessionEntryJson.WriteOptionalGuid(writer, "causalParentId", entry.CausalParentId?.Value);
        writer.WriteString("recordedAt", entry.RecordedAt.ToString("O", CultureInfo.InvariantCulture));
        WriteState(writer, entry.State);
        writer.WriteEndObject();
    }

    private static void WriteState(Utf8JsonWriter writer, SessionAcceptedRunState state)
    {
        Debug.Assert(writer is not null && state is not null, "Validated state is required.");
        writer.WritePropertyName("state");
        writer.WriteStartObject();
        WriteAddress(writer, "address", state.Address);
        PortableSessionEntryJson.WriteGuid(writer, "executionLaneId", state.ExecutionLaneId.Value);
        writer.WriteNumber("laneRevision", state.LaneRevision.Value);
        WriteCorrelation(writer, "correlation", state.Correlation);
        writer.WriteNumber("operationStateRevision", state.OperationStateRevision.Value);
        PortableSessionSecurityJson.WriteIdentity(writer, state.Identity);
        PortableSessionSecurityJson.WriteAuthorization(writer, state.Authorization);
        WriteProfile(writer, state.SessionProfile);
        WriteConfiguration(writer, state.Configuration);
        WriteCursor(writer, "previousCursor", state.PreviousCursor);
        WriteCursor(writer, "committedCursor", state.CommittedCursor);
        writer.WriteNumber("promotionCutoff", state.PromotionCutoff.Value);
        PortableSessionEntryJson.WriteGuid(writer, "initiatingAdmissionId", state.InitiatingAdmissionId.Value);
        WriteGuidArray(writer, "promotedAdmissionIds", state.PromotedAdmissionIds, static value => value.Value);
        WriteGuidArray(writer, "materializedEntryIds", state.MaterializedEntryIds, static value => value.Value);
        WriteGuidArray(writer, "materializedMessageIds", state.MaterializedMessageIds, static value => value.Value);
        PortableSessionEntryJson.WriteGuid(writer, "initialTurnId", state.InitialTurnId.Value);
        writer.WriteString("acceptedAt", state.AcceptedAt.ToString("O", CultureInfo.InvariantCulture));
        writer.WriteString("state", "accepted");
        writer.WriteEndObject();
    }

    private bool Valid(OperationAcceptedSessionEntry entry)
    {
        if (entry.Id == default || entry.Address is null || entry.Address.AgentId == default || entry.Address.SessionId == default
            || entry.Correlation is not InRunOperationCorrelation correlation || correlation.OperationId == default
            || correlation.RunId == default || correlation.TurnId is null || correlation.TurnId.Value == default
            || entry.BranchId == default || entry.Sequence.Value <= 0 || entry.CausalParentId is null
            || entry.CausalParentId.Value == default || entry.SchemaVersion != _version || entry.State is null)
        {
            return false;
        }
        var state = entry.State;
        var arrayCount = (long) state.PromotedAdmissionIds.Length + state.MaterializedEntryIds.Length
            + state.MaterializedMessageIds.Length;
        return arrayCount * 39 <= _limits.MaximumPayloadBytes
            && state.SessionProfile.Key.Value is { } profileKey
            && state.Configuration.Fingerprint.Value is { } fingerprint
            && (long) profileKey.Length + fingerprint.Length <= _limits.MaximumPayloadBytes
            && PortableSessionEntryJson.IsValidText(profileKey)
            && PortableSessionEntryJson.IsValidText(fingerprint)
            && PortableSessionSecurityJson.ValidForEncode(state.Identity, state.Authorization, _limits) && state.Address == entry.Address && state.Correlation == correlation && state.AcceptedAt == entry.RecordedAt
            && state.CommittedCursor.BranchId == entry.BranchId && state.CommittedCursor.LastEntryId == entry.Id
            && !state.MaterializedEntryIds.IsDefaultOrEmpty && entry.CausalParentId == state.MaterializedEntryIds[^1]
            && state.Authorization.Scope.AgentId == state.Address.AgentId
            && state.Authorization.Scope.SessionId == state.Address.SessionId
            && state.Authorization.Scope.Correlation == state.Correlation
            && state.Authorization.ConfigurationVersion == state.Configuration.ConfigurationVersion;
    }

    private bool TryAddress(JsonElement parent, string name, ref int count, ref int bytes, out SessionAddress address)
    {
        address = null!;
        return PortableSessionEntryJson.TryObject(parent, name, out var item)
            && PortableSessionEntryJson.ValidateObject(item, _address, _limits, ref count, ref bytes)
            && PortableSessionEntryJson.TryGuid(item, "agentId", out var agent)
            && PortableSessionEntryJson.TryGuid(item, "sessionId", out var session)
            && Assign(new SessionAddress(new AgentId(agent), new SessionId(session)), out address);
    }

    private bool TryCorrelation(JsonElement parent, string name, ref int count, ref int bytes, out InRunOperationCorrelation correlation)
    {
        correlation = null!;
        return PortableSessionEntryJson.TryObject(parent, name, out var item)
            && PortableSessionEntryJson.ValidateObject(item, _correlation, _limits, ref count, ref bytes)
            && PortableSessionEntryJson.TryString(item, "kind", out var kind) && kind == "inRun"
            && PortableSessionEntryJson.TryGuid(item, "operationId", out var operation)
            && PortableSessionEntryJson.TryGuid(item, "runId", out var run)
            && PortableSessionEntryJson.TryGuid(item, "turnId", out var turn)
            && Assign(new InRunOperationCorrelation(new OperationId(operation), new RunId(run), new TurnId(turn)), out correlation);
    }

    private bool TryProfile(JsonElement state, ref int count, ref int bytes, out SessionProfileReference profile)
    {
        profile = null!;
        return PortableSessionEntryJson.TryObject(state, "sessionProfile", out var item)
            && PortableSessionEntryJson.ValidateObject(item, _profile, _limits, ref count, ref bytes)
            && PortableSessionEntryJson.TryString(item, "key", out var key)
            && PortableSessionEntryJson.TryInt64(item, "version", out var version) && version > 0
            && Assign(new SessionProfileReference(new SessionProfileKey(key), new SessionProfileVersion(version)), out profile);
    }

    private bool TryConfiguration(JsonElement state, ref int count, ref int bytes, out RunConfigurationReference configuration)
    {
        configuration = null!;
        return PortableSessionEntryJson.TryObject(state, "configuration", out var item)
            && PortableSessionEntryJson.ValidateObject(item, _configuration, _limits, ref count, ref bytes)
            && PortableSessionEntryJson.TryInt64(item, "configurationVersion", out var configurationVersion) && configurationVersion > 0
            && PortableSessionEntryJson.TryInt64(item, "policyVersion", out var policyVersion) && policyVersion > 0
            && PortableSessionEntryJson.TryString(item, "fingerprint", out var fingerprint)
            && Assign(new RunConfigurationReference(new ConfigurationVersion(configurationVersion),
                new RunPolicyVersion(policyVersion), new ContentHash(fingerprint)), out configuration);
    }

    private bool TryCursor(JsonElement state, string name, ref int count, ref int bytes, out SessionBranchCursor cursor)
    {
        cursor = null!;
        return PortableSessionEntryJson.TryObject(state, name, out var item)
            && PortableSessionEntryJson.ValidateObject(item, _cursor, _limits, ref count, ref bytes)
            && PortableSessionEntryJson.TryGuid(item, "branchId", out var branch)
            && PortableSessionEntryJson.TryOptionalGuid(item, "lastEntryId", out var last)
            && Assign(new SessionBranchCursor(new BranchId(branch), last is { } value ? new SessionEntryId(value) : null), out cursor);
    }

    private static bool TryGuidArray<T>(JsonElement parent, string name, Func<Guid, T> factory, out ImmutableArray<T> values)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(name), "The caller supplies one schema field name.");
        Debug.Assert(factory is not null, "The caller supplies one value constructor.");
        values = default;
        if (!parent.TryGetProperty(name, out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var builder = ImmutableArray.CreateBuilder<T>();
        var identities = new HashSet<Guid>();
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || !Guid.TryParseExact(item.GetString(), "D", out var value)
                || value == Guid.Empty || item.GetString() != value.ToString("D") || !identities.Add(value))
            {
                return false;
            }

            builder.Add(factory(value));
        }
        values = builder.ToImmutable();
        return !values.IsDefaultOrEmpty;
    }

    private static void WriteAddress(Utf8JsonWriter writer, string name, SessionAddress address)
    {
        Debug.Assert(writer is not null && address is not null, "Validated address is required.");
        writer.WritePropertyName(name);
        writer.WriteStartObject();
        PortableSessionEntryJson.WriteGuid(writer, "agentId", address.AgentId.Value);
        PortableSessionEntryJson.WriteGuid(writer, "sessionId", address.SessionId.Value);
        writer.WriteEndObject();
    }

    private static void WriteCorrelation(Utf8JsonWriter writer, string name, InRunOperationCorrelation correlation)
    {
        Debug.Assert(writer is not null && correlation is not null && correlation.TurnId is not null, "Validated in-run correlation is required.");
        writer.WritePropertyName(name);
        writer.WriteStartObject();
        writer.WriteString("kind", "inRun");
        PortableSessionEntryJson.WriteGuid(writer, "operationId", correlation.OperationId.Value);
        PortableSessionEntryJson.WriteGuid(writer, "runId", correlation.RunId.Value);
        PortableSessionEntryJson.WriteGuid(writer, "turnId", correlation.TurnId.Value.Value);
        writer.WriteEndObject();
    }

    private static void WriteProfile(Utf8JsonWriter writer, SessionProfileReference profile)
    {
        Debug.Assert(writer is not null && profile is not null, "Validated profile evidence is required.");
        writer.WritePropertyName("sessionProfile");
        writer.WriteStartObject();
        writer.WriteString("key", profile.Key.Value);
        writer.WriteNumber("version", profile.Version.Value);
        writer.WriteEndObject();
    }
    private static void WriteConfiguration(Utf8JsonWriter writer, RunConfigurationReference configuration)
    {
        Debug.Assert(writer is not null && configuration is not null, "Validated configuration evidence is required.");
        writer.WritePropertyName("configuration");
        writer.WriteStartObject();
        writer.WriteNumber("configurationVersion", configuration.ConfigurationVersion.Value);
        writer.WriteNumber("policyVersion", configuration.PolicyVersion.Value);
        writer.WriteString("fingerprint", configuration.Fingerprint.Value);
        writer.WriteEndObject();
    }
    private static void WriteCursor(Utf8JsonWriter writer, string name, SessionBranchCursor cursor)
    {
        Debug.Assert(writer is not null && !string.IsNullOrWhiteSpace(name) && cursor is not null,
            "Validated cursor evidence and its schema name are required.");
        writer.WritePropertyName(name);
        writer.WriteStartObject();
        PortableSessionEntryJson.WriteGuid(writer, "branchId", cursor.BranchId.Value);
        PortableSessionEntryJson.WriteOptionalGuid(writer, "lastEntryId", cursor.LastEntryId?.Value);
        writer.WriteEndObject();
    }
    private static void WriteGuidArray<T>(Utf8JsonWriter writer, string name, ImmutableArray<T> values, Func<T, Guid> selector)
    {
        Debug.Assert(writer is not null && !string.IsNullOrWhiteSpace(name) && !values.IsDefault && selector is not null,
            "Validated ordered identities and their schema name are required.");
        writer.WritePropertyName(name);
        writer.WriteStartArray();
        foreach (var value in values)
        {
            writer.WriteStringValue(selector(value).ToString("D"));
        }

        writer.WriteEndArray();
    }

    private static bool Assign<T>(T source, out T value)
    {
        value = source;
        return true;
    }
    private static FrozenSet<string> Set(params string[] names) => names.ToFrozenSet(StringComparer.Ordinal);

    private static FrozenDictionary<string, FrozenSet<string>> CreateSchema()
    {
        var schema = new Dictionary<string, FrozenSet<string>>(StringComparer.Ordinal)
        {
            [string.Empty] = _root,
            ["address"] = _address,
            ["correlation"] = _correlation,
            ["state"] = _state,
            ["state/address"] = _address,
            ["state/correlation"] = _correlation,
            ["state/sessionProfile"] = _profile,
            ["state/configuration"] = _configuration,
            ["state/previousCursor"] = _cursor,
            ["state/committedCursor"] = _cursor,
        };
        PortableSessionSecurityJson.AddSchema(schema, "state");
        return schema.ToFrozenDictionary(StringComparer.Ordinal);
    }
}
