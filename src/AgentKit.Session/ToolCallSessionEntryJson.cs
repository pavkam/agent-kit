// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session;

using System.Globalization;
using System.Text.Json;

/// <summary>Reads and writes the explicit portable v1 tool-call evidence shared by the accepted and terminal session-entry codecs.</summary>
/// <remarks>
/// The helpers never activate CLR types, resolve a security authority, or mint a grant. Result content, arguments, usage,
/// and extension data are not part of the schema; the codecs refuse to encode a value that carries them rather than drop
/// them silently.
/// </remarks>
internal static class ToolCallSessionEntryJson
{
    private static readonly FrozenSet<string> _root = Set("id", "address", "correlation", "branchId", "sequence", "causalParentId", "recordedAt", "state");
    private static readonly FrozenSet<string> _address = Set("agentId", "sessionId");
    private static readonly FrozenSet<string> _correlation = Set("kind", "operationId", "runId", "turnId");
    private static readonly FrozenSet<string> _state = Set("address", "correlation", "identity", "authorization", "call");
    private static readonly FrozenSet<string> _acceptance = Set("grantId", "validatedArgumentsFingerprint", "acceptedAt");
    private static readonly FrozenSet<string> _admission = Set("catalogVersion", "sourceOrdinal", "rawArgumentsFingerprint");
    private static readonly FrozenSet<string> _effects = Set("effect", "idempotency", "resourceKinds");
    private static readonly FrozenSet<string> _policy = Set("key", "version");
    private static readonly FrozenSet<string> _normalization = Set("rejectionPolicy", "projectionPolicy", "executionPolicy", "algorithmVersion", "bounds", "allowedTransformations");
    private static readonly FrozenSet<string> _bounds = Set("maximumCanonicalBytes", "maximumParts");
    private static readonly FrozenSet<string> _normalizationInfo = Set("transformations", "inputCanonicalBytes", "inputParts", "omittedCanonicalBytes", "omittedParts");
    private static readonly FrozenSet<string> _error = Set("kind", "safeMessage", "externalCode", "retryAfterTicks");

    /// <summary>Gets the fixed JSON discriminator written for in-run correlations.</summary>
    internal const string InRunKind = "inRun";

    /// <summary>Adds every nested object path shared by both tool-call payloads.</summary>
    /// <param name="schema">The mutable path map being assembled for one concrete entry codec.</param>
    /// <param name="callFields">The exact property names of the codec's own <c>state/call</c> object.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    internal static void AddSchema(IDictionary<string, FrozenSet<string>> schema, FrozenSet<string> callFields)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(callFields);
        schema[string.Empty] = _root;
        schema["address"] = _address;
        schema["correlation"] = _correlation;
        schema["state"] = _state;
        schema["state/address"] = _address;
        schema["state/correlation"] = _correlation;
        schema["state/call"] = callFields;
        schema["state/call/acceptance"] = _acceptance;
        schema["state/call/admission"] = _admission;
        schema["state/call/effects"] = _effects;
        schema["state/call/normalization"] = _normalization;
        schema["state/call/normalization/rejectionPolicy"] = _policy;
        schema["state/call/normalization/projectionPolicy"] = _policy;
        schema["state/call/normalization/executionPolicy"] = _policy;
        schema["state/call/normalization/bounds"] = _bounds;
        schema["state/call/normalizationInfo"] = _normalizationInfo;
        schema["state/call/error"] = _error;
        schema["state/call/projectionPolicy"] = _policy;
        PortableSessionSecurityJson.AddSchema(schema, "state");
    }

    /// <summary>Creates a frozen ordinal property-name set.</summary>
    /// <param name="names">The exact property names.</param>
    /// <returns>The immutable set.</returns>
    internal static FrozenSet<string> Set(params string[] names) => names.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>Holds the envelope fields common to every tool-call entry payload.</summary>
    /// <param name="Id">The entry identity.</param><param name="Address">The owning session.</param>
    /// <param name="Correlation">The in-run correlation.</param><param name="BranchId">The branch.</param>
    /// <param name="Sequence">The branch-local sequence.</param><param name="CausalParentId">The optional causal parent.</param>
    /// <param name="RecordedAt">The commit time.</param><param name="Call">The codec-specific call object.</param>
    /// <param name="Authorization">The reconstructed authorization evidence.</param>
    internal sealed record Envelope(
        SessionEntryId Id,
        SessionAddress Address,
        InRunOperationCorrelation Correlation,
        BranchId BranchId,
        SessionSequence Sequence,
        SessionEntryId? CausalParentId,
        DateTimeOffset RecordedAt,
        JsonElement Call,
        SecurityAuthorizationContext Authorization);

    /// <summary>Validates and reads the shared envelope, identity, and authorization.</summary>
    /// <param name="root">The parsed payload root.</param>
    /// <param name="limits">The captured codec limits.</param>
    /// <param name="count">The cumulative unknown-field count.</param>
    /// <param name="bytes">The cumulative unknown bytes.</param>
    /// <param name="envelope">The reconstructed envelope on success.</param>
    /// <returns>Whether every shared field is present, canonical, and mutually consistent.</returns>
    internal static bool TryReadEnvelope(JsonElement root, SessionEntryCodecLimits limits, ref int count, ref int bytes, out Envelope envelope)
    {
        ArgumentNullException.ThrowIfNull(limits);
        envelope = null!;
        if (!PortableSessionEntryJson.ValidateObject(root, _root, limits, ref count, ref bytes)
            || !PortableSessionEntryJson.TryGuid(root, "id", out var id)
            || !TryAddress(root, "address", limits, ref count, ref bytes, out var address)
            || !TryCorrelation(root, "correlation", limits, ref count, ref bytes, out var correlation)
            || !PortableSessionEntryJson.TryGuid(root, "branchId", out var branch)
            || !PortableSessionEntryJson.TryInt64(root, "sequence", out var sequence) || sequence <= 0
            || !PortableSessionEntryJson.TryOptionalGuid(root, "causalParentId", out var parent)
            || !PortableSessionEntryJson.TryTimestamp(root, "recordedAt", out var recordedAt)
            || !PortableSessionEntryJson.TryObject(root, "state", out var state)
            || !PortableSessionEntryJson.ValidateObject(state, _state, limits, ref count, ref bytes)
            || !TryAddress(state, "address", limits, ref count, ref bytes, out var stateAddress) || stateAddress != address
            || !TryCorrelation(state, "correlation", limits, ref count, ref bytes, out var stateCorrelation) || stateCorrelation != correlation
            || !PortableSessionSecurityJson.TryIdentity(state, limits, ref count, ref bytes, out var identity)
            || !PortableSessionSecurityJson.TryAuthorization(state, identity, address, correlation, limits, ref count, ref bytes, out var authorization)
            || !PortableSessionEntryJson.TryObject(state, "call", out var call))
        {
            return false;
        }

        envelope = new Envelope(
            new SessionEntryId(id), address, correlation, new BranchId(branch), new SessionSequence(sequence),
            parent is { } value ? new SessionEntryId(value) : null, recordedAt, call, authorization);
        return true;
    }

    /// <summary>Writes the shared envelope and opens the <c>call</c> object; the caller writes its fields and then calls <see cref="WriteEnd"/>.</summary>
    /// <param name="writer">The live bounded writer.</param>
    /// <param name="entry">The validated entry.</param>
    /// <param name="correlation">The entry's validated in-run correlation.</param>
    /// <param name="authorization">The historical authorization evidence the entry carries.</param>
    internal static void WriteStart(Utf8JsonWriter writer, SessionEntry entry, InRunOperationCorrelation correlation, SecurityAuthorizationContext authorization)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(correlation);
        ArgumentNullException.ThrowIfNull(authorization);
        writer.WriteStartObject();
        PortableSessionEntryJson.WriteGuid(writer, "id", entry.Id.Value);
        WriteAddress(writer, "address", entry.Address);
        WriteCorrelation(writer, "correlation", correlation);
        PortableSessionEntryJson.WriteGuid(writer, "branchId", entry.BranchId.Value);
        writer.WriteNumber("sequence", entry.Sequence.Value);
        PortableSessionEntryJson.WriteOptionalGuid(writer, "causalParentId", entry.CausalParentId?.Value);
        writer.WriteString("recordedAt", entry.RecordedAt.ToString("O", CultureInfo.InvariantCulture));
        writer.WritePropertyName("state");
        writer.WriteStartObject();
        WriteAddress(writer, "address", entry.Address);
        WriteCorrelation(writer, "correlation", correlation);
        PortableSessionSecurityJson.WriteIdentity(writer, authorization.Identity);
        PortableSessionSecurityJson.WriteAuthorization(writer, authorization);
        writer.WritePropertyName("call");
        writer.WriteStartObject();
    }

    /// <summary>Closes the <c>call</c>, <c>state</c>, and root objects opened by <see cref="WriteStart"/>.</summary>
    /// <param name="writer">The live bounded writer.</param>
    internal static void WriteEnd(Utf8JsonWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteEndObject();
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    /// <summary>Returns whether an entry's shared fields satisfy the v1 encode preconditions.</summary>
    /// <param name="entry">The entry to inspect.</param>
    /// <param name="schemaVersion">The schema version the codec writes.</param>
    /// <param name="authorization">The authorization evidence the entry's record carries.</param>
    /// <param name="limits">The captured codec limits.</param>
    /// <returns><see langword="true"/> when identities, correlation, authorization, and text are encodable.</returns>
    internal static bool ValidEnvelope(SessionEntry entry, SchemaVersion schemaVersion, SecurityAuthorizationContext authorization, SessionEntryCodecLimits limits)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(authorization);
        ArgumentNullException.ThrowIfNull(limits);
        return entry.Id != default
            && entry.Address is { } address && address.AgentId != default && address.SessionId != default
            && entry.Correlation is InRunOperationCorrelation { TurnId: not null } correlation
            && correlation.OperationId != default && correlation.RunId != default && correlation.TurnId.Value != default
            && entry.BranchId != default && entry.Sequence.Value > 0
            && (entry.CausalParentId is null || entry.CausalParentId.Value != default)
            && entry.SchemaVersion == schemaVersion
            && authorization.Scope.AgentId == address.AgentId
            && authorization.Scope.SessionId == address.SessionId
            && authorization.Scope.Correlation == correlation
            && PortableSessionSecurityJson.ValidForEncode(authorization.Identity, authorization, limits);
    }

    /// <summary>Returns whether every text value the evidence retains is encodable and within the payload bound.</summary>
    /// <param name="limits">The captured codec limits.</param>
    /// <param name="texts">The text values to inspect; null entries are skipped.</param>
    /// <returns><see langword="true"/> when every text is lossless UTF-8 and the total fits.</returns>
    internal static bool ValidTexts(SessionEntryCodecLimits limits, params ReadOnlySpan<string?> texts)
    {
        ArgumentNullException.ThrowIfNull(limits);
        long total = 0;
        foreach (var text in texts)
        {
            if (text is null)
            {
                continue;
            }

            total += text.Length;
            if (!PortableSessionEntryJson.IsValidText(text))
            {
                return false;
            }
        }

        return total <= limits.MaximumPayloadBytes / 4;
    }

    /// <summary>Reads a required boolean.</summary>
    /// <param name="parent">The containing object.</param><param name="name">The exact field name.</param><param name="value">The boolean on success.</param>
    /// <returns>Whether the field is a JSON boolean.</returns>
    internal static bool TryBool(JsonElement parent, string name, out bool value)
    {
        value = false;
        if (!parent.TryGetProperty(name, out var property) || property.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            return false;
        }

        value = property.GetBoolean();
        return true;
    }

    /// <summary>Reads a required field containing JSON null or a string.</summary>
    /// <param name="parent">The containing object.</param><param name="name">The exact field name.</param><param name="value">The optional string.</param>
    /// <returns>Whether the field is null or a string.</returns>
    internal static bool TryNullableString(JsonElement parent, string name, out string? value)
    {
        value = null;
        if (!parent.TryGetProperty(name, out var property))
        {
            return false;
        }

        if (property.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (property.ValueKind != JsonValueKind.String || property.GetString() is not { } text)
        {
            return false;
        }

        value = text;
        return true;
    }

    /// <summary>Reads a required field containing JSON null or an exact Int64.</summary>
    /// <param name="parent">The containing object.</param><param name="name">The exact field name.</param><param name="value">The optional integer.</param>
    /// <returns>Whether the field is null or an in-range integer.</returns>
    internal static bool TryNullableInt64(JsonElement parent, string name, out long? value)
    {
        value = null;
        if (!parent.TryGetProperty(name, out var property))
        {
            return false;
        }

        if (property.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (property.ValueKind != JsonValueKind.Number || !property.TryGetInt64(out var number))
        {
            return false;
        }

        value = number;
        return true;
    }

    /// <summary>Reads a required field containing JSON null or a canonical timestamp.</summary>
    /// <param name="parent">The containing object.</param><param name="name">The exact field name.</param><param name="value">The optional timestamp.</param>
    /// <returns>Whether the field is null or a canonical timestamp.</returns>
    internal static bool TryNullableTimestamp(JsonElement parent, string name, out DateTimeOffset? value)
    {
        value = null;
        if (!parent.TryGetProperty(name, out var property))
        {
            return false;
        }

        if (property.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (!PortableSessionEntryJson.TryTimestamp(parent, name, out var timestamp))
        {
            return false;
        }

        value = timestamp;
        return true;
    }

    /// <summary>Reads a required optional nested object.</summary>
    /// <param name="parent">The containing object.</param><param name="name">The exact field name.</param><param name="value">The object when present.</param>
    /// <param name="present">Whether the object was present rather than null.</param>
    /// <returns>Whether the field is null or an object.</returns>
    internal static bool TryNullableObject(JsonElement parent, string name, out JsonElement value, out bool present)
    {
        value = default;
        present = false;
        if (!parent.TryGetProperty(name, out var property))
        {
            return false;
        }

        if (property.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (property.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        value = property;
        present = true;
        return true;
    }

    /// <summary>Writes a timestamp in exact round-trip format.</summary>
    /// <param name="writer">The live writer.</param><param name="name">The field name.</param><param name="value">The timestamp.</param>
    internal static void WriteTimestamp(Utf8JsonWriter writer, string name, DateTimeOffset value) =>
        writer.WriteString(name, value.ToString("O", CultureInfo.InvariantCulture));

    /// <summary>Writes an optional timestamp as canonical text or null.</summary>
    /// <param name="writer">The live writer.</param><param name="name">The field name.</param><param name="value">The optional timestamp.</param>
    internal static void WriteNullableTimestamp(Utf8JsonWriter writer, string name, DateTimeOffset? value)
    {
        if (value is { } present)
        {
            WriteTimestamp(writer, name, present);
        }
        else
        {
            writer.WriteNull(name);
        }
    }

    /// <summary>Writes an optional string or null.</summary>
    /// <param name="writer">The live writer.</param><param name="name">The field name.</param><param name="value">The optional text.</param>
    internal static void WriteNullableString(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is null)
        {
            writer.WriteNull(name);
        }
        else
        {
            writer.WriteString(name, value);
        }
    }

    /// <summary>Writes an optional Int64 or null.</summary>
    /// <param name="writer">The live writer.</param><param name="name">The field name.</param><param name="value">The optional integer.</param>
    internal static void WriteNullableInt64(Utf8JsonWriter writer, string name, long? value)
    {
        if (value is { } present)
        {
            writer.WriteNumber(name, present);
        }
        else
        {
            writer.WriteNull(name);
        }
    }

    /// <summary>Writes the admission evidence object.</summary>
    /// <param name="writer">The live writer.</param><param name="admission">The admission evidence.</param>
    internal static void WriteAdmission(Utf8JsonWriter writer, ToolCallAdmissionEvidence admission)
    {
        writer.WritePropertyName("admission");
        writer.WriteStartObject();
        writer.WriteString("catalogVersion", admission.CatalogVersion.Value);
        writer.WriteNumber("sourceOrdinal", admission.SourceOrdinal);
        writer.WriteString("rawArgumentsFingerprint", admission.RawArgumentsFingerprint.Value);
        writer.WriteEndObject();
    }

    /// <summary>Reads the admission evidence object.</summary>
    /// <param name="parent">The call object.</param><param name="limits">The codec limits.</param><param name="count">The unknown-field count.</param><param name="bytes">The unknown bytes.</param>
    /// <param name="admission">The evidence on success.</param><returns>Whether the object is valid.</returns>
    internal static bool TryAdmission(JsonElement parent, SessionEntryCodecLimits limits, ref int count, ref int bytes, out ToolCallAdmissionEvidence admission)
    {
        admission = null!;
        if (!PortableSessionEntryJson.TryObject(parent, "admission", out var item)
            || !PortableSessionEntryJson.ValidateObject(item, _admission, limits, ref count, ref bytes)
            || !PortableSessionEntryJson.TryString(item, "catalogVersion", out var catalog)
            || !PortableSessionEntryJson.TryInt64(item, "sourceOrdinal", out var ordinal) || ordinal is < 0 or > int.MaxValue
            || !PortableSessionEntryJson.TryString(item, "rawArgumentsFingerprint", out var fingerprint))
        {
            return false;
        }

        admission = new ToolCallAdmissionEvidence(new ToolCatalogVersion(catalog), (int) ordinal, new InputFingerprint(fingerprint));
        return true;
    }

    /// <summary>Writes optional acceptance evidence as an object or null.</summary>
    /// <param name="writer">The live writer.</param><param name="acceptance">The optional acceptance evidence.</param>
    internal static void WriteAcceptance(Utf8JsonWriter writer, ToolCallAcceptanceEvidence? acceptance)
    {
        if (acceptance is null)
        {
            writer.WriteNull("acceptance");
            return;
        }

        writer.WritePropertyName("acceptance");
        writer.WriteStartObject();
        PortableSessionEntryJson.WriteGuid(writer, "grantId", acceptance.InvocationGrantId.Value);
        writer.WriteString("validatedArgumentsFingerprint", acceptance.ValidatedArgumentsFingerprint.Value);
        WriteTimestamp(writer, "acceptedAt", acceptance.AcceptedAt);
        writer.WriteEndObject();
    }

    /// <summary>Reads optional acceptance evidence.</summary>
    /// <param name="parent">The call object.</param><param name="limits">The codec limits.</param><param name="count">The unknown-field count.</param><param name="bytes">The unknown bytes.</param>
    /// <param name="acceptance">The evidence when present.</param><returns>Whether the field is null or a valid object.</returns>
    internal static bool TryAcceptance(JsonElement parent, SessionEntryCodecLimits limits, ref int count, ref int bytes, out ToolCallAcceptanceEvidence? acceptance)
    {
        acceptance = null;
        if (!TryNullableObject(parent, "acceptance", out var item, out var present))
        {
            return false;
        }

        if (!present)
        {
            return true;
        }

        if (!PortableSessionEntryJson.ValidateObject(item, _acceptance, limits, ref count, ref bytes)
            || !PortableSessionEntryJson.TryGuid(item, "grantId", out var grant)
            || !PortableSessionEntryJson.TryString(item, "validatedArgumentsFingerprint", out var fingerprint)
            || !PortableSessionEntryJson.TryTimestamp(item, "acceptedAt", out var acceptedAt))
        {
            return false;
        }

        acceptance = new ToolCallAcceptanceEvidence(new GrantId(grant), new InputFingerprint(fingerprint), acceptedAt);
        return true;
    }

    /// <summary>Writes optional declared effects as an object or null.</summary>
    /// <param name="writer">The live writer.</param><param name="effects">The optional declared effects.</param>
    internal static void WriteEffects(Utf8JsonWriter writer, ToolEffects? effects)
    {
        if (effects is null)
        {
            writer.WriteNull("effects");
            return;
        }

        writer.WritePropertyName("effects");
        writer.WriteStartObject();
        writer.WriteNumber("effect", (int) effects.Effect);
        WriteNullableInt64(writer, "idempotency", effects.Idempotency is { } idempotency ? (int) idempotency : null);
        if (effects.RequiredResourceKinds is { } kinds)
        {
            writer.WritePropertyName("resourceKinds");
            writer.WriteStartArray();
            foreach (var kind in kinds)
            {
                writer.WriteNumberValue((int) kind);
            }

            writer.WriteEndArray();
        }
        else
        {
            writer.WriteNull("resourceKinds");
        }

        writer.WriteEndObject();
    }

    /// <summary>Reads optional declared effects.</summary>
    /// <param name="parent">The call object.</param><param name="limits">The codec limits.</param><param name="count">The unknown-field count.</param><param name="bytes">The unknown bytes.</param>
    /// <param name="effects">The effects when present.</param><returns>Whether the field is null or a valid object.</returns>
    internal static bool TryEffects(JsonElement parent, SessionEntryCodecLimits limits, ref int count, ref int bytes, out ToolEffects? effects)
    {
        effects = null;
        if (!TryNullableObject(parent, "effects", out var item, out var present))
        {
            return false;
        }

        if (!present)
        {
            return true;
        }

        if (!PortableSessionEntryJson.ValidateObject(item, _effects, limits, ref count, ref bytes)
            || !PortableSessionEntryJson.TryInt64(item, "effect", out var effect) || !Enum.IsDefined((ToolEffect) effect)
            || !TryNullableInt64(item, "idempotency", out var idempotency)
            || (idempotency is { } value && !Enum.IsDefined((IdempotencyClassification) value))
            || !item.TryGetProperty("resourceKinds", out var kindsElement))
        {
            return false;
        }

        ImmutableArray<ProtectedResourceKind>? kinds = null;
        if (kindsElement.ValueKind == JsonValueKind.Array)
        {
            var builder = ImmutableArray.CreateBuilder<ProtectedResourceKind>();
            foreach (var kind in kindsElement.EnumerateArray())
            {
                if (kind.ValueKind != JsonValueKind.Number || !kind.TryGetInt32(out var number) || !Enum.IsDefined((ProtectedResourceKind) number))
                {
                    return false;
                }

                builder.Add((ProtectedResourceKind) number);
            }

            kinds = builder.ToImmutable();
        }
        else if (kindsElement.ValueKind != JsonValueKind.Null)
        {
            return false;
        }

        effects = new ToolEffects((ToolEffect) effect, idempotency is { } code ? (IdempotencyClassification) code : null, kinds);
        return true;
    }

    /// <summary>Writes one retained policy reference as an object.</summary>
    /// <param name="writer">The live writer.</param><param name="name">The field name.</param><param name="key">The key text.</param><param name="version">The positive version.</param>
    internal static void WritePolicy(Utf8JsonWriter writer, string name, string? key, long version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        writer.WritePropertyName(name);
        writer.WriteStartObject();
        writer.WriteString("key", key);
        writer.WriteNumber("version", version);
        writer.WriteEndObject();
    }

    /// <summary>Reads one retained policy reference object.</summary>
    /// <param name="parent">The containing object.</param><param name="name">The field name.</param><param name="limits">The codec limits.</param><param name="count">The unknown-field count.</param><param name="bytes">The unknown bytes.</param>
    /// <param name="key">The key text.</param><param name="version">The positive version.</param><returns>Whether the object is valid.</returns>
    internal static bool TryPolicy(JsonElement parent, string name, SessionEntryCodecLimits limits, ref int count, ref int bytes, out string key, out long version)
    {
        key = string.Empty;
        version = 0;
        return PortableSessionEntryJson.TryObject(parent, name, out var item)
            && PortableSessionEntryJson.ValidateObject(item, _policy, limits, ref count, ref bytes)
            && PortableSessionEntryJson.TryString(item, "key", out key)
            && PortableSessionEntryJson.TryInt64(item, "version", out version) && version > 0;
    }

    /// <summary>Writes the normalization snapshot object; callers have already refused extension data.</summary>
    /// <param name="writer">The live writer.</param><param name="snapshot">The snapshot.</param>
    internal static void WriteNormalization(Utf8JsonWriter writer, ToolResultNormalizationSnapshot snapshot)
    {
        writer.WritePropertyName("normalization");
        writer.WriteStartObject();
        WritePolicy(writer, "rejectionPolicy", snapshot.RejectionPolicy.Key.Value, snapshot.RejectionPolicy.Version.Value);
        WritePolicy(writer, "projectionPolicy", snapshot.ProjectionPolicy.Key.Value, snapshot.ProjectionPolicy.Version.Value);
        if (snapshot.ExecutionPolicy is { } execution)
        {
            WritePolicy(writer, "executionPolicy", execution.Key.Value, execution.Version.Value);
        }
        else
        {
            writer.WriteNull("executionPolicy");
        }

        writer.WriteNumber("algorithmVersion", snapshot.AlgorithmVersion.Value);
        writer.WritePropertyName("bounds");
        writer.WriteStartObject();
        writer.WriteNumber("maximumCanonicalBytes", snapshot.Bounds.MaximumCanonicalBytes);
        writer.WriteNumber("maximumParts", snapshot.Bounds.MaximumParts);
        writer.WriteEndObject();
        writer.WriteNumber("allowedTransformations", (int) snapshot.AllowedTransformations);
        writer.WriteEndObject();
    }

    /// <summary>Reads the normalization snapshot object.</summary>
    /// <param name="parent">The call object.</param><param name="limits">The codec limits.</param><param name="count">The unknown-field count.</param><param name="bytes">The unknown bytes.</param>
    /// <param name="snapshot">The snapshot on success.</param><returns>Whether the object is valid.</returns>
    internal static bool TryNormalization(JsonElement parent, SessionEntryCodecLimits limits, ref int count, ref int bytes, out ToolResultNormalizationSnapshot snapshot)
    {
        snapshot = null!;
        if (!PortableSessionEntryJson.TryObject(parent, "normalization", out var item)
            || !PortableSessionEntryJson.ValidateObject(item, _normalization, limits, ref count, ref bytes)
            || !TryPolicy(item, "rejectionPolicy", limits, ref count, ref bytes, out var rejectionKey, out var rejectionVersion)
            || !TryPolicy(item, "projectionPolicy", limits, ref count, ref bytes, out var projectionKey, out var projectionVersion)
            || !TryNullableObject(item, "executionPolicy", out var execution, out var hasExecution)
            || !PortableSessionEntryJson.TryInt64(item, "algorithmVersion", out var algorithm) || algorithm <= 0
            || !PortableSessionEntryJson.TryObject(item, "bounds", out var bounds)
            || !PortableSessionEntryJson.ValidateObject(bounds, _bounds, limits, ref count, ref bytes)
            || !PortableSessionEntryJson.TryInt64(bounds, "maximumCanonicalBytes", out var maximumBytes) || maximumBytes <= 0
            || !PortableSessionEntryJson.TryInt64(bounds, "maximumParts", out var maximumParts) || maximumParts is <= 0 or > int.MaxValue
            || !PortableSessionEntryJson.TryInt64(item, "allowedTransformations", out var transformations) || transformations is < 0 or > int.MaxValue)
        {
            return false;
        }

        ToolExecutionPolicyReference? executionPolicy = null;
        if (hasExecution)
        {
            _ = execution;
            if (!TryPolicy(item, "executionPolicy", limits, ref count, ref bytes, out var executionKey, out var executionVersion))
            {
                return false;
            }

            executionPolicy = new ToolExecutionPolicyReference(new ToolExecutionPolicyKey(executionKey), new ToolExecutionPolicyVersion(executionVersion));
        }

        snapshot = new ToolResultNormalizationSnapshot(
            new ToolResultRejectionPolicyReference(new ToolResultRejectionPolicyKey(rejectionKey), new ToolResultRejectionPolicyVersion(rejectionVersion)),
            new ToolResultProjectionPolicyReference(new ToolResultProjectionPolicyKey(projectionKey), new ToolResultProjectionPolicyVersion(projectionVersion)),
            executionPolicy,
            new ToolResultNormalizationAlgorithmVersion(algorithm),
            new ToolResultBounds(maximumBytes, (int) maximumParts),
            (ToolResultProjectionTransformations) transformations,
            ExtensionData.Empty);
        return true;
    }

    /// <summary>Writes the terminal normalization info object; callers have already refused extension data.</summary>
    /// <param name="writer">The live writer.</param><param name="info">The info.</param>
    internal static void WriteNormalizationInfo(Utf8JsonWriter writer, ToolResultNormalizationInfo info)
    {
        writer.WritePropertyName("normalizationInfo");
        writer.WriteStartObject();
        writer.WritePropertyName("transformations");
        writer.WriteStartArray();
        foreach (var transformation in info.Transformations)
        {
            writer.WriteNumberValue((int) transformation);
        }

        writer.WriteEndArray();
        WriteNullableInt64(writer, "inputCanonicalBytes", info.InputCanonicalBytes);
        WriteNullableInt64(writer, "inputParts", info.InputParts);
        WriteNullableInt64(writer, "omittedCanonicalBytes", info.OmittedCanonicalBytes);
        WriteNullableInt64(writer, "omittedParts", info.OmittedParts);
        writer.WriteEndObject();
    }

    /// <summary>Reads the terminal normalization info object.</summary>
    /// <param name="parent">The call object.</param><param name="limits">The codec limits.</param><param name="count">The unknown-field count.</param><param name="bytes">The unknown bytes.</param>
    /// <param name="info">The info on success.</param><returns>Whether the object is valid.</returns>
    internal static bool TryNormalizationInfo(JsonElement parent, SessionEntryCodecLimits limits, ref int count, ref int bytes, out ToolResultNormalizationInfo info)
    {
        info = null!;
        if (!PortableSessionEntryJson.TryObject(parent, "normalizationInfo", out var item)
            || !PortableSessionEntryJson.ValidateObject(item, _normalizationInfo, limits, ref count, ref bytes)
            || !item.TryGetProperty("transformations", out var array) || array.ValueKind != JsonValueKind.Array
            || !TryNullableInt64(item, "inputCanonicalBytes", out var inputBytes)
            || !TryNullableInt64(item, "inputParts", out var inputParts) || inputParts is > int.MaxValue
            || !TryNullableInt64(item, "omittedCanonicalBytes", out var omittedBytes)
            || !TryNullableInt64(item, "omittedParts", out var omittedParts) || omittedParts is > int.MaxValue)
        {
            return false;
        }

        var transformations = ImmutableArray.CreateBuilder<ToolResultNormalizationTransformation>();
        foreach (var element in array.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt32(out var number)
                || !Enum.IsDefined((ToolResultNormalizationTransformation) number))
            {
                return false;
            }

            transformations.Add((ToolResultNormalizationTransformation) number);
        }

        info = new ToolResultNormalizationInfo(
            transformations.ToImmutable(), inputBytes, (int?) inputParts, omittedBytes, (int?) omittedParts, ExtensionData.Empty);
        return true;
    }

    /// <summary>Writes an optional safe error as an object or null; callers have already refused extension data.</summary>
    /// <param name="writer">The live writer.</param><param name="error">The optional error.</param>
    internal static void WriteError(Utf8JsonWriter writer, ToolError? error)
    {
        if (error is null)
        {
            writer.WriteNull("error");
            return;
        }

        writer.WritePropertyName("error");
        writer.WriteStartObject();
        writer.WriteNumber("kind", (int) error.Kind);
        writer.WriteString("safeMessage", error.SafeMessage);
        WriteNullableString(writer, "externalCode", error.ExternalCode);
        WriteNullableInt64(writer, "retryAfterTicks", error.RetryAfter?.Ticks);
        writer.WriteEndObject();
    }

    /// <summary>Reads an optional safe error.</summary>
    /// <param name="parent">The call object.</param><param name="limits">The codec limits.</param><param name="count">The unknown-field count.</param><param name="bytes">The unknown bytes.</param>
    /// <param name="error">The error when present.</param><returns>Whether the field is null or a valid object.</returns>
    internal static bool TryError(JsonElement parent, SessionEntryCodecLimits limits, ref int count, ref int bytes, out ToolError? error)
    {
        error = null;
        if (!TryNullableObject(parent, "error", out var item, out var present))
        {
            return false;
        }

        if (!present)
        {
            return true;
        }

        if (!PortableSessionEntryJson.ValidateObject(item, _error, limits, ref count, ref bytes)
            || !PortableSessionEntryJson.TryInt64(item, "kind", out var kind) || !Enum.IsDefined((ToolErrorKind) kind)
            || !PortableSessionEntryJson.TryString(item, "safeMessage", out var message)
            || !TryNullableString(item, "externalCode", out var code)
            || !TryNullableInt64(item, "retryAfterTicks", out var ticks))
        {
            return false;
        }

        error = new ToolError((ToolErrorKind) kind, message, code, ticks is { } value ? TimeSpan.FromTicks(value) : null, ExtensionData.Empty);
        return true;
    }

    private static bool TryAddress(JsonElement parent, string name, SessionEntryCodecLimits limits, ref int count, ref int bytes, out SessionAddress address)
    {
        address = null!;
        if (!PortableSessionEntryJson.TryObject(parent, name, out var item)
            || !PortableSessionEntryJson.ValidateObject(item, _address, limits, ref count, ref bytes)
            || !PortableSessionEntryJson.TryGuid(item, "agentId", out var agent)
            || !PortableSessionEntryJson.TryGuid(item, "sessionId", out var session))
        {
            return false;
        }

        address = new SessionAddress(new AgentId(agent), new SessionId(session));
        return true;
    }

    private static bool TryCorrelation(JsonElement parent, string name, SessionEntryCodecLimits limits, ref int count, ref int bytes, out InRunOperationCorrelation correlation)
    {
        correlation = null!;
        if (!PortableSessionEntryJson.TryObject(parent, name, out var item)
            || !PortableSessionEntryJson.ValidateObject(item, _correlation, limits, ref count, ref bytes)
            || !PortableSessionEntryJson.TryString(item, "kind", out var kind) || kind != InRunKind
            || !PortableSessionEntryJson.TryGuid(item, "operationId", out var operation)
            || !PortableSessionEntryJson.TryGuid(item, "runId", out var run)
            || !PortableSessionEntryJson.TryGuid(item, "turnId", out var turn))
        {
            return false;
        }

        correlation = new InRunOperationCorrelation(new OperationId(operation), new RunId(run), new TurnId(turn));
        return true;
    }

    private static void WriteAddress(Utf8JsonWriter writer, string name, SessionAddress address)
    {
        writer.WritePropertyName(name);
        writer.WriteStartObject();
        PortableSessionEntryJson.WriteGuid(writer, "agentId", address.AgentId.Value);
        PortableSessionEntryJson.WriteGuid(writer, "sessionId", address.SessionId.Value);
        writer.WriteEndObject();
    }

    private static void WriteCorrelation(Utf8JsonWriter writer, string name, InRunOperationCorrelation correlation)
    {
        Debug.Assert(correlation.TurnId is not null, "The validated in-run correlation names a turn.");
        writer.WritePropertyName(name);
        writer.WriteStartObject();
        writer.WriteString("kind", InRunKind);
        PortableSessionEntryJson.WriteGuid(writer, "operationId", correlation.OperationId.Value);
        PortableSessionEntryJson.WriteGuid(writer, "runId", correlation.RunId.Value);
        PortableSessionEntryJson.WriteGuid(writer, "turnId", correlation.TurnId.Value.Value);
        writer.WriteEndObject();
    }
}
