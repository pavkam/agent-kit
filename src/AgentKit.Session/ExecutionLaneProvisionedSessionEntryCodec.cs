// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session;

using System.Globalization;
using System.Text.Json;

/// <summary>Encodes and decodes the explicit portable v1 execution-lane provisioning schema.</summary>
/// <remarks>This thread-safe codec performs no CLR activation and reconstructs evidence values only; decoded claims confer no execution or security authority.</remarks>
public sealed class ExecutionLaneProvisionedSessionEntryCodec: ISessionEntryCodec
{
    private static readonly SchemaVersion _version = new("1");
    private static readonly SessionEntryCodecLimits _defaultLimits = new(1_048_576, 64, 65_536, 16);
    private static readonly FrozenSet<string> _root = new[] { "id", "address", "correlation", "branchId", "sequence", "causalParentId", "recordedAt", "executionLaneId", "laneRevision", "sessionProfile", "configuration" }.ToFrozenSet(StringComparer.Ordinal);
    private static readonly FrozenSet<string> _address = new[] { "agentId", "sessionId" }.ToFrozenSet(StringComparer.Ordinal);
    private static readonly FrozenSet<string> _correlation = new[] { "kind", "operationId", "admissionId" }.ToFrozenSet(StringComparer.Ordinal);
    private static readonly FrozenSet<string> _profile = new[] { "key", "version" }.ToFrozenSet(StringComparer.Ordinal);
    private static readonly FrozenSet<string> _configuration = new[] { "configurationVersion", "policyVersion", "fingerprint" }.ToFrozenSet(StringComparer.Ordinal);
    private static readonly FrozenDictionary<string, FrozenSet<string>> _fieldsByPath =
        new Dictionary<string, FrozenSet<string>>(StringComparer.Ordinal)
        {
            [string.Empty] = _root,
            ["address"] = _address,
            ["correlation"] = _correlation,
            ["sessionProfile"] = _profile,
            ["configuration"] = _configuration,
        }.ToFrozenDictionary(StringComparer.Ordinal);
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ExecutionLaneProvisionedSessionEntryCodec> _logger;
    private readonly SessionEntryCodecLimits _limits;

    /// <summary>Creates a thread-safe codec with explicit timing and content-free diagnostics.</summary>
    /// <param name="timeProvider">The non-null clock used only for best-effort duration observation.</param>
    /// <param name="logger">The non-null structured logger; entry content is never emitted.</param>
    /// <exception cref="ArgumentNullException">A parameter is null.</exception>
    public ExecutionLaneProvisionedSessionEntryCodec(TimeProvider timeProvider, ILogger<ExecutionLaneProvisionedSessionEntryCodec> logger)
        : this(timeProvider, logger, _defaultLimits)
    {
    }

    /// <summary>Creates a thread-safe codec with explicit immutable payload, extension, and depth limits.</summary>
    /// <param name="timeProvider">The non-null clock used only for best-effort duration observation.</param>
    /// <param name="logger">The non-null structured logger; entry content is never emitted.</param>
    /// <param name="limits">The non-null validated limits captured by this codec and its descriptor.</param>
    /// <exception cref="ArgumentNullException">A parameter is null.</exception>
    public ExecutionLaneProvisionedSessionEntryCodec(TimeProvider timeProvider,
        ILogger<ExecutionLaneProvisionedSessionEntryCodec> logger, SessionEntryCodecLimits limits)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(limits);
        _timeProvider = timeProvider;
        _logger = logger;
        _limits = limits;
        Descriptor = new(new SessionEntryTypeId("agentkit.session/execution-lane-provisioned"),
            typeof(ExecutionLaneProvisionedSessionEntry), _version, [_version], limits);
    }

    /// <inheritdoc/>
    public SessionEntryCodecDescriptor Descriptor { get; }

    /// <inheritdoc/>
    public SessionEntryEncodeResult Encode(SessionEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var evidence = entry is ExecutionLaneProvisionedSessionEntry value && Valid(value) ? value : null;
        return SessionEntryCodecObservation.Observe(_timeProvider, _logger, "execution-lane-provisioned.encode",
            evidence, () => EncodeCore(entry));
    }

    /// <inheritdoc/>
    public SessionEntryDecodeResult Decode(SessionEntryWireEnvelope wire)
    {
        ArgumentNullException.ThrowIfNull(wire);
        return SessionEntryCodecObservation.Observe(_timeProvider, _logger, "execution-lane-provisioned.decode",
            null, () => DecodeCore(wire));
    }

    private SessionEntryEncodeResult EncodeCore(SessionEntry entry)
    {
        Debug.Assert(entry is not null, "The public encode boundary rejects null entries.");
        if (entry is not ExecutionLaneProvisionedSessionEntry value || !Valid(value))
        {
            return new SessionEntryEncodeRejected("The entry does not satisfy the execution-lane provisioning v1 schema.");
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

    private void Write(Utf8JsonWriter writer, ExecutionLaneProvisionedSessionEntry value)
    {
        Debug.Assert(writer is not null, "The caller owns a live bounded JSON writer.");
        Debug.Assert(Valid(value), "The entry passed complete schema validation before serialization.");
        var correlation = (BeforeRunOperationCorrelation) value.Correlation;
        writer.WriteStartObject();
        PortableSessionEntryJson.WriteGuid(writer, "id", value.Id.Value);
        WriteAddress(writer, value.Address);
        writer.WritePropertyName("correlation");
        writer.WriteStartObject();
        writer.WriteString("kind", "beforeRun");
        PortableSessionEntryJson.WriteGuid(writer, "operationId", correlation.OperationId.Value);
        PortableSessionEntryJson.WriteOptionalGuid(writer, "admissionId", correlation.AdmissionId?.Value);
        writer.WriteEndObject();
        PortableSessionEntryJson.WriteGuid(writer, "branchId", value.BranchId.Value);
        writer.WriteNumber("sequence", value.Sequence.Value);
        PortableSessionEntryJson.WriteOptionalGuid(writer, "causalParentId", value.CausalParentId?.Value);
        writer.WriteString("recordedAt", value.RecordedAt.ToString("O", CultureInfo.InvariantCulture));
        PortableSessionEntryJson.WriteGuid(writer, "executionLaneId", value.ExecutionLaneId.Value);
        writer.WriteNumber("laneRevision", value.LaneRevision.Value);
        writer.WritePropertyName("sessionProfile");
        writer.WriteStartObject();
        writer.WriteString("key", value.SessionProfile.Key.Value);
        writer.WriteNumber("version", value.SessionProfile.Version.Value);
        writer.WriteEndObject();
        writer.WritePropertyName("configuration");
        writer.WriteStartObject();
        writer.WriteNumber("configurationVersion", value.Configuration.ConfigurationVersion.Value);
        writer.WriteNumber("policyVersion", value.Configuration.PolicyVersion.Value);
        writer.WriteString("fingerprint", value.Configuration.Fingerprint.Value);
        writer.WriteEndObject();
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
                var unknownCount = 0;
                var unknownBytes = 0;
                if (!PortableSessionEntryJson.ValidateObject(root, _root, _limits, ref unknownCount, ref unknownBytes)
                    || !PortableSessionEntryJson.TryGuid(root, "id", out var id)
                    || !TryAddress(root, ref unknownCount, ref unknownBytes, out var address)
                    || !TryCorrelation(root, ref unknownCount, ref unknownBytes, out var correlation)
                    || !PortableSessionEntryJson.TryGuid(root, "branchId", out var branch)
                    || !PortableSessionEntryJson.TryInt64(root, "sequence", out var sequence) || sequence <= 0
                    || !PortableSessionEntryJson.TryOptionalGuid(root, "causalParentId", out var parent)
                    || !PortableSessionEntryJson.TryTimestamp(root, "recordedAt", out var recordedAt)
                    || !PortableSessionEntryJson.TryGuid(root, "executionLaneId", out var lane)
                    || !PortableSessionEntryJson.TryInt64(root, "laneRevision", out var laneRevision) || laneRevision <= 0
                    || !TryProfile(root, ref unknownCount, ref unknownBytes, out var profile)
                    || !TryConfiguration(root, ref unknownCount, ref unknownBytes, out var configuration))
                {
                    return new SessionEntryDecodeRejected("The execution-lane provisioning payload is malformed.");
                }

                var entry = new ExecutionLaneProvisionedSessionEntry(new SessionEntryId(id), address, correlation,
                    new BranchId(branch), new SessionSequence(sequence), parent is { } p ? new SessionEntryId(p) : null,
                    recordedAt, _version, new ExecutionLaneId(lane), new SessionLaneRevision(laneRevision), profile, configuration);
                return new SessionEntryDecoded(new DecodedSessionEntry(entry, wire));
            }
            catch (Exception exception) when (exception is ArgumentException or OverflowException)
            {
                return new SessionEntryDecodeRejected("The execution-lane provisioning payload violates semantic constraints.");
            }
        }
    }

    private bool Valid(ExecutionLaneProvisionedSessionEntry value) => value.Id != default
        && value.Address is not null && value.Address.AgentId != default && value.Address.SessionId != default
        && value.Correlation is BeforeRunOperationCorrelation { OperationId: var operation } correlation && operation != default
        && (correlation.AdmissionId is null || correlation.AdmissionId.Value != default)
        && value.BranchId != default && value.Sequence.Value > 0
        && (value.CausalParentId is null || value.CausalParentId.Value != default)
        && value.ExecutionLaneId != default && value.LaneRevision != default
        && value.SessionProfile?.Key.Value is { } profileKey
        && value.Configuration?.Fingerprint.Value is { } fingerprint
        && (long) profileKey.Length + fingerprint.Length <= _limits.MaximumPayloadBytes
        && PortableSessionEntryJson.IsValidText(profileKey)
        && PortableSessionEntryJson.IsValidText(fingerprint)
        && value.SchemaVersion == _version;

    private static void WriteAddress(Utf8JsonWriter writer, SessionAddress address)
    {
        Debug.Assert(writer is not null, "The caller owns a live bounded JSON writer.");
        Debug.Assert(address is not null, "The entry passed address validation.");
        writer.WritePropertyName("address");
        writer.WriteStartObject();
        PortableSessionEntryJson.WriteGuid(writer, "agentId", address.AgentId.Value);
        PortableSessionEntryJson.WriteGuid(writer, "sessionId", address.SessionId.Value);
        writer.WriteEndObject();
    }

    private bool TryAddress(JsonElement root, ref int count, ref int bytes, out SessionAddress address)
    {
        address = null!;
        return PortableSessionEntryJson.TryObject(root, "address", out var item)
            && PortableSessionEntryJson.ValidateObject(item, _address, _limits, ref count, ref bytes)
            && PortableSessionEntryJson.TryGuid(item, "agentId", out var agent)
            && PortableSessionEntryJson.TryGuid(item, "sessionId", out var session)
            && Assign(new SessionAddress(new AgentId(agent), new SessionId(session)), out address);
    }

    private bool TryCorrelation(JsonElement root, ref int count, ref int bytes, out BeforeRunOperationCorrelation correlation)
    {
        correlation = null!;
        return PortableSessionEntryJson.TryObject(root, "correlation", out var item)
            && PortableSessionEntryJson.ValidateObject(item, _correlation, _limits, ref count, ref bytes)
            && PortableSessionEntryJson.TryString(item, "kind", out var kind)
            && kind == "beforeRun"
            && PortableSessionEntryJson.TryGuid(item, "operationId", out var operation)
            && PortableSessionEntryJson.TryOptionalGuid(item, "admissionId", out var admission)
            && Assign(new BeforeRunOperationCorrelation(
                new OperationId(operation), admission is { } value ? new AdmissionId(value) : null), out correlation);
    }

    private bool TryProfile(JsonElement root, ref int count, ref int bytes, out SessionProfileReference profile)
    {
        profile = null!;
        return PortableSessionEntryJson.TryObject(root, "sessionProfile", out var item)
            && PortableSessionEntryJson.ValidateObject(item, _profile, _limits, ref count, ref bytes)
            && PortableSessionEntryJson.TryString(item, "key", out var key)
            && PortableSessionEntryJson.TryInt64(item, "version", out var version)
            && version > 0
            && Assign(new SessionProfileReference(new SessionProfileKey(key), new SessionProfileVersion(version)), out profile);
    }

    private bool TryConfiguration(JsonElement root, ref int count, ref int bytes, out RunConfigurationReference configuration)
    {
        configuration = null!;
        return PortableSessionEntryJson.TryObject(root, "configuration", out var item)
            && PortableSessionEntryJson.ValidateObject(item, _configuration, _limits, ref count, ref bytes)
            && PortableSessionEntryJson.TryInt64(item, "configurationVersion", out var configurationVersion)
            && configurationVersion > 0
            && PortableSessionEntryJson.TryInt64(item, "policyVersion", out var policyVersion)
            && policyVersion > 0
            && PortableSessionEntryJson.TryString(item, "fingerprint", out var hash)
            && Assign(new RunConfigurationReference(new ConfigurationVersion(configurationVersion),
                new RunPolicyVersion(policyVersion), new ContentHash(hash)), out configuration);
    }

    private static bool Assign<T>(T source, out T value)
    {
        value = source;
        return true;
    }
}
