// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session;

using System.Globalization;
using System.Text.Json;

/// <summary>Reads and writes the exact immutable identity and authorization evidence embedded in session recovery records.</summary>
/// <remarks>The helper reconstructs evidence values only. It never resolves an authority, validates credentials, or creates a grant.</remarks>
internal static class PortableSessionSecurityJson
{
    private static readonly FrozenSet<string> _identity = Set("tenantId", "principalId", "subjectKind", "evidence", "claims", "delegationChain", "assurance", "version");
    private static readonly FrozenSet<string> _evidence = Set("id", "issuer", "method", "authenticatedAt", "expiresAt", "safeFingerprint");
    private static readonly FrozenSet<string> _claim = Set("issuer", "type", "value", "valueKind");
    private static readonly FrozenSet<string> _delegation = Set("id", "tenantId", "principalId", "issuer", "evidenceId", "version", "delegatedAt", "claims", "assurance");
    private static readonly FrozenSet<string> _authorization = Set("profileKey", "profileVersion", "policySnapshot", "authorityKey", "agentDefinitionRevision", "configurationVersion", "scope", "identityRef");
    private static readonly FrozenSet<string> _policy = Set("id", "version", "fingerprint");
    private static readonly FrozenSet<string> _scope = Set("agentId", "sessionId", "correlationRef");

    /// <summary>Adds every known nested object path used by identity and authorization evidence.</summary>
    /// <param name="schema">The mutable path map being assembled for one concrete entry codec.</param>
    /// <param name="statePath">The path of the accepted-state object.</param>
    internal static void AddSchema(IDictionary<string, FrozenSet<string>> schema, string statePath)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentException.ThrowIfNullOrWhiteSpace(statePath);
        schema[$"{statePath}/identity"] = _identity;
        schema[$"{statePath}/identity/evidence"] = _evidence;
        schema[$"{statePath}/identity/claims"] = _claim;
        schema[$"{statePath}/identity/delegationChain"] = _delegation;
        schema[$"{statePath}/identity/delegationChain/claims"] = _claim;
        schema[$"{statePath}/authorization"] = _authorization;
        schema[$"{statePath}/authorization/policySnapshot"] = _policy;
        schema[$"{statePath}/authorization/scope"] = _scope;
    }

    /// <summary>Validates that retained security text is losslessly encodable and cannot exceed the payload before serialization.</summary>
    /// <param name="identity">The immutable identity to inspect.</param><param name="authorization">The immutable authorization evidence.</param>
    /// <param name="limits">The captured payload limits.</param><returns>Whether all text and collection lower bounds fit the codec.</returns>
    internal static bool ValidForEncode(ExecutionIdentity identity, SecurityAuthorizationContext authorization,
        SessionEntryCodecLimits limits)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(authorization);
        ArgumentNullException.ThrowIfNull(limits);
        if (identity.Claims.IsDefault || identity.DelegationChain.IsDefault
            || identity.Claims.Length > limits.MaximumPayloadBytes / 48
            || identity.DelegationChain.Length > limits.MaximumPayloadBytes / 128)
        {
            return false;
        }

        long characters = 0;
        if (!ValidText(identity.TenantId.Value, limits, ref characters)
            || !ValidText(identity.PrincipalId.Value, limits, ref characters)
            || !ValidText(identity.Evidence.Id.Value, limits, ref characters)
            || !ValidText(identity.Evidence.Issuer.Value, limits, ref characters)
            || !ValidText(identity.Evidence.Method, limits, ref characters)
            || !ValidText(identity.Evidence.SafeFingerprint.Hash.Value, limits, ref characters)
            || !ValidText(authorization.ProfileKey.Value, limits, ref characters)
            || !ValidText(authorization.PolicySnapshot.Fingerprint.Value, limits, ref characters)
            || !ValidText(authorization.AuthorityKey.Value, limits, ref characters))
        {
            return false;
        }

        foreach (var claim in identity.Claims)
        {
            if (!Enum.IsDefined(claim.ValueKind)
                || !ValidText(claim.Issuer.Value, limits, ref characters)
                || !ValidText(claim.Type, limits, ref characters)
                || !ValidText(claim.Value, limits, ref characters))
            {
                return false;
            }
        }

        foreach (var link in identity.DelegationChain)
        {
            if (!Enum.IsDefined(link.Assurance) || link.Claims.IsDefault
                || link.Claims.Length > limits.MaximumPayloadBytes / 48
                || !ValidText(link.TenantId.Value, limits, ref characters)
                || !ValidText(link.PrincipalId.Value, limits, ref characters)
                || !ValidText(link.Issuer.Value, limits, ref characters)
                || !ValidText(link.EvidenceId.Value, limits, ref characters))
            {
                return false;
            }

            foreach (var claim in link.Claims)
            {
                if (!Enum.IsDefined(claim.ValueKind)
                    || !ValidText(claim.Issuer.Value, limits, ref characters)
                    || !ValidText(claim.Type, limits, ref characters)
                    || !ValidText(claim.Value, limits, ref characters))
                {
                    return false;
                }
            }
        }

        return Enum.IsDefined(identity.SubjectKind) && Enum.IsDefined(identity.Assurance);
    }

    private static bool ValidText(string? text, SessionEntryCodecLimits limits, ref long characters) =>
        text is not null && (characters += text.Length) <= limits.MaximumPayloadBytes
        && PortableSessionEntryJson.IsValidText(text);

    /// <summary>Writes a complete immutable execution identity using fixed discriminants and ordered arrays.</summary>
    /// <param name="writer">The live bounded writer.</param><param name="identity">The validated identity evidence.</param>
    internal static void WriteIdentity(Utf8JsonWriter writer, ExecutionIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(identity);
        writer.WritePropertyName("identity");
        writer.WriteStartObject();
        writer.WriteString("tenantId", identity.TenantId.Value);
        writer.WriteString("principalId", identity.PrincipalId.Value);
        writer.WriteString("subjectKind", Subject(identity.SubjectKind));
        WriteEvidence(writer, identity.Evidence);
        WriteClaims(writer, "claims", identity.Claims);
        writer.WritePropertyName("delegationChain");
        writer.WriteStartArray();
        foreach (var link in identity.DelegationChain)
        {
            writer.WriteStartObject();
            PortableSessionEntryJson.WriteGuid(writer, "id", link.Id.Value);
            writer.WriteString("tenantId", link.TenantId.Value);
            writer.WriteString("principalId", link.PrincipalId.Value);
            writer.WriteString("issuer", link.Issuer.Value);
            writer.WriteString("evidenceId", link.EvidenceId.Value);
            writer.WriteNumber("version", link.Version.Value);
            writer.WriteString("delegatedAt", link.DelegatedAt.ToString("O", CultureInfo.InvariantCulture));
            WriteClaims(writer, "claims", link.Claims);
            writer.WriteString("assurance", Assurance(link.Assurance));
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteString("assurance", Assurance(identity.Assurance));
        writer.WriteNumber("version", identity.Version.Value);
        writer.WriteEndObject();
    }

    /// <summary>Writes complete authorization evidence while referencing the accepted state's single identity and correlation values.</summary>
    /// <param name="writer">The live bounded writer.</param><param name="authorization">The validated authorization evidence.</param>
    internal static void WriteAuthorization(Utf8JsonWriter writer, SecurityAuthorizationContext authorization)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(authorization);
        writer.WritePropertyName("authorization");
        writer.WriteStartObject();
        writer.WriteString("profileKey", authorization.ProfileKey.Value);
        writer.WriteNumber("profileVersion", authorization.ProfileVersion.Value);
        writer.WritePropertyName("policySnapshot");
        writer.WriteStartObject();
        PortableSessionEntryJson.WriteGuid(writer, "id", authorization.PolicySnapshot.Id.Value);
        writer.WriteNumber("version", authorization.PolicySnapshot.Version.Value);
        writer.WriteString("fingerprint", authorization.PolicySnapshot.Fingerprint.Value);
        writer.WriteEndObject();
        writer.WriteString("authorityKey", authorization.AuthorityKey.Value);
        writer.WriteNumber("agentDefinitionRevision", authorization.AgentDefinitionRevision.Value);
        writer.WriteNumber("configurationVersion", authorization.ConfigurationVersion.Value);
        writer.WritePropertyName("scope");
        writer.WriteStartObject();
        PortableSessionEntryJson.WriteGuid(writer, "agentId", authorization.Scope.AgentId.Value);
        PortableSessionEntryJson.WriteOptionalGuid(writer, "sessionId", authorization.Scope.SessionId?.Value);
        writer.WriteString("correlationRef", "state.correlation");
        writer.WriteEndObject();
        writer.WriteString("identityRef", "state.identity");
        writer.WriteEndObject();
    }

    /// <summary>Reads and validates one complete execution identity, preserving claim and delegation order.</summary>
    /// <param name="state">The accepted-state object.</param><param name="limits">The captured codec limits.</param>
    /// <param name="count">The cumulative unknown-field count.</param><param name="bytes">The cumulative unknown bytes.</param>
    /// <param name="identity">The reconstructed evidence on success.</param><returns>Whether the identity is complete and valid.</returns>
    internal static bool TryIdentity(JsonElement state, SessionEntryCodecLimits limits, ref int count, ref int bytes, out ExecutionIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(limits);
        identity = null!;
        if (!PortableSessionEntryJson.TryObject(state, "identity", out var item)
            || !PortableSessionEntryJson.ValidateObject(item, _identity, limits, ref count, ref bytes)
            || !PortableSessionEntryJson.TryString(item, "tenantId", out var tenant)
            || !PortableSessionEntryJson.TryString(item, "principalId", out var principal)
            || !PortableSessionEntryJson.TryString(item, "subjectKind", out var subjectText)
            || !TrySubject(subjectText, out var subject)
            || !TryEvidence(item, limits, ref count, ref bytes, out var evidence)
            || !TryClaims(item, "claims", limits, ref count, ref bytes, out var claims)
            || !TryDelegations(item, limits, ref count, ref bytes, out var delegations)
            || !PortableSessionEntryJson.TryString(item, "assurance", out var assuranceText)
            || !TryAssurance(assuranceText, out var assurance)
            || !PortableSessionEntryJson.TryInt64(item, "version", out var version) || version <= 0)
        {
            return false;
        }
        identity = new ExecutionIdentity(new TenantId(tenant), new PrincipalId(principal), subject, evidence,
            claims, delegations, assurance, new IdentityVersion(version));
        return true;
    }

    /// <summary>Reads authorization evidence and binds its explicit references to the already validated state identity and correlation.</summary>
    /// <param name="state">The accepted-state object.</param><param name="identity">The exact state identity.</param>
    /// <param name="address">The exact state address.</param><param name="correlation">The exact state correlation.</param>
    /// <param name="limits">The captured codec limits.</param><param name="count">The cumulative unknown count.</param>
    /// <param name="bytes">The cumulative unknown bytes.</param><param name="authorization">The reconstructed evidence on success.</param>
    /// <returns>Whether all authorization fields and references are valid.</returns>
    internal static bool TryAuthorization(JsonElement state, ExecutionIdentity identity, SessionAddress address,
        InRunOperationCorrelation correlation, SessionEntryCodecLimits limits, ref int count, ref int bytes,
        out SecurityAuthorizationContext authorization)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(correlation);
        ArgumentNullException.ThrowIfNull(limits);
        authorization = null!;
        if (!PortableSessionEntryJson.TryObject(state, "authorization", out var item)
            || !PortableSessionEntryJson.ValidateObject(item, _authorization, limits, ref count, ref bytes)
            || !PortableSessionEntryJson.TryString(item, "profileKey", out var profileKey)
            || !PortableSessionEntryJson.TryInt64(item, "profileVersion", out var profileVersion) || profileVersion <= 0
            || !TryPolicy(item, limits, ref count, ref bytes, out var policy)
            || !PortableSessionEntryJson.TryString(item, "authorityKey", out var authorityKey)
            || !PortableSessionEntryJson.TryInt64(item, "agentDefinitionRevision", out var definitionRevision) || definitionRevision < 0
            || !PortableSessionEntryJson.TryInt64(item, "configurationVersion", out var configurationVersion) || configurationVersion <= 0
            || !TryScope(item, address, correlation, limits, ref count, ref bytes, out var scope)
            || !PortableSessionEntryJson.TryString(item, "identityRef", out var identityRef) || identityRef != "state.identity")
        {
            return false;
        }
        authorization = new SecurityAuthorizationContext(new SecurityProfileKey(profileKey),
            new SecurityProfileVersion(profileVersion), policy, new ComponentKey<ISecurityAuthority>(authorityKey),
            new AgentDefinitionRevision(definitionRevision), new ConfigurationVersion(configurationVersion), scope, identity);
        return true;
    }

    private static void WriteEvidence(Utf8JsonWriter writer, AuthenticationEvidence evidence)
    {
        Debug.Assert(writer is not null && evidence is not null, "Validated identity evidence is required.");
        writer.WritePropertyName("evidence");
        writer.WriteStartObject();
        writer.WriteString("id", evidence.Id.Value);
        writer.WriteString("issuer", evidence.Issuer.Value);
        writer.WriteString("method", evidence.Method);
        writer.WriteString("authenticatedAt", evidence.AuthenticatedAt.ToString("O", CultureInfo.InvariantCulture));
        if (evidence.ExpiresAt is { } expiry)
        {
            writer.WriteString("expiresAt", expiry.ToString("O", CultureInfo.InvariantCulture));
        }
        else
        {
            writer.WriteNull("expiresAt");
        }

        writer.WriteString("safeFingerprint", evidence.SafeFingerprint.Hash.Value);
        writer.WriteEndObject();
    }

    private static void WriteClaims(Utf8JsonWriter writer, string name, ImmutableArray<IdentityClaim> claims)
    {
        Debug.Assert(writer is not null && !string.IsNullOrWhiteSpace(name) && !claims.IsDefault, "Validated ordered claims are required.");
        writer.WritePropertyName(name);
        writer.WriteStartArray();
        foreach (var claim in claims)
        {
            writer.WriteStartObject();
            writer.WriteString("issuer", claim.Issuer.Value);
            writer.WriteString("type", claim.Type);
            writer.WriteString("value", claim.Value);
            writer.WriteString("valueKind", ClaimKind(claim.ValueKind));
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }

    private static bool TryEvidence(JsonElement identity, SessionEntryCodecLimits limits, ref int count, ref int bytes, out AuthenticationEvidence evidence)
    {
        evidence = null!;
        if (!PortableSessionEntryJson.TryObject(identity, "evidence", out var item)
            || !PortableSessionEntryJson.ValidateObject(item, _evidence, limits, ref count, ref bytes)
            || !PortableSessionEntryJson.TryString(item, "id", out var id)
            || !PortableSessionEntryJson.TryString(item, "issuer", out var issuer)
            || !PortableSessionEntryJson.TryString(item, "method", out var method)
            || !PortableSessionEntryJson.TryTimestamp(item, "authenticatedAt", out var authenticatedAt)
            || !TryOptionalTimestamp(item, "expiresAt", out var expiresAt)
            || !PortableSessionEntryJson.TryString(item, "safeFingerprint", out var fingerprint))
        {
            return false;
        }

        evidence = new AuthenticationEvidence(new AuthenticationEvidenceId(id), new IdentityIssuerId(issuer), method,
            authenticatedAt, expiresAt, new AuthenticationEvidenceFingerprint(new ContentHash(fingerprint)));
        return true;
    }

    private static bool TryClaims(JsonElement parent, string name, SessionEntryCodecLimits limits, ref int count, ref int bytes, out ImmutableArray<IdentityClaim> claims)
    {
        claims = default;
        if (!parent.TryGetProperty(name, out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var builder = ImmutableArray.CreateBuilder<IdentityClaim>();
        foreach (var item in array.EnumerateArray())
        {
            if (!PortableSessionEntryJson.ValidateObject(item, _claim, limits, ref count, ref bytes)
                || !PortableSessionEntryJson.TryString(item, "issuer", out var issuer)
                || !PortableSessionEntryJson.TryString(item, "type", out var type)
                || !PortableSessionEntryJson.TryString(item, "value", out var value)
                || !PortableSessionEntryJson.TryString(item, "valueKind", out var kindText)
                || !TryClaimKind(kindText, out var kind))
            {
                return false;
            }

            builder.Add(new IdentityClaim(new IdentityIssuerId(issuer), type, value, kind));
        }
        claims = builder.ToImmutable();
        return true;
    }

    private static bool TryDelegations(JsonElement identity, SessionEntryCodecLimits limits, ref int count, ref int bytes, out ImmutableArray<DelegationIdentityLink> links)
    {
        links = default;
        if (!identity.TryGetProperty("delegationChain", out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var builder = ImmutableArray.CreateBuilder<DelegationIdentityLink>();
        foreach (var item in array.EnumerateArray())
        {
            if (!PortableSessionEntryJson.ValidateObject(item, _delegation, limits, ref count, ref bytes)
                || !PortableSessionEntryJson.TryGuid(item, "id", out var id)
                || !PortableSessionEntryJson.TryString(item, "tenantId", out var tenant)
                || !PortableSessionEntryJson.TryString(item, "principalId", out var principal)
                || !PortableSessionEntryJson.TryString(item, "issuer", out var issuer)
                || !PortableSessionEntryJson.TryString(item, "evidenceId", out var evidenceId)
                || !PortableSessionEntryJson.TryInt64(item, "version", out var version) || version <= 0
                || !PortableSessionEntryJson.TryTimestamp(item, "delegatedAt", out var delegatedAt)
                || !TryClaims(item, "claims", limits, ref count, ref bytes, out var claims)
                || !PortableSessionEntryJson.TryString(item, "assurance", out var assuranceText)
                || !TryAssurance(assuranceText, out var assurance))
            {
                return false;
            }

            builder.Add(new DelegationIdentityLink(new DelegationId(id), new TenantId(tenant), new PrincipalId(principal),
                new IdentityIssuerId(issuer), new AuthenticationEvidenceId(evidenceId), new IdentityVersion(version),
                delegatedAt, claims, assurance));
        }
        links = builder.ToImmutable();
        return true;
    }

    private static bool TryPolicy(JsonElement authorization, SessionEntryCodecLimits limits, ref int count, ref int bytes, out SecurityPolicySnapshotReference policy)
    {
        policy = null!;
        if (!PortableSessionEntryJson.TryObject(authorization, "policySnapshot", out var item)
            || !PortableSessionEntryJson.ValidateObject(item, _policy, limits, ref count, ref bytes)
            || !PortableSessionEntryJson.TryGuid(item, "id", out var id)
            || !PortableSessionEntryJson.TryInt64(item, "version", out var version) || version <= 0
            || !PortableSessionEntryJson.TryString(item, "fingerprint", out var fingerprint))
        {
            return false;
        }

        policy = new SecurityPolicySnapshotReference(new SecurityPolicySnapshotId(id), new SecurityPolicyVersion(version), new ContentHash(fingerprint));
        return true;
    }

    private static bool TryScope(JsonElement authorization, SessionAddress address, InRunOperationCorrelation correlation,
        SessionEntryCodecLimits limits, ref int count, ref int bytes, out SecurityAuthorizationScope scope)
    {
        scope = null!;
        if (!PortableSessionEntryJson.TryObject(authorization, "scope", out var item)
            || !PortableSessionEntryJson.ValidateObject(item, _scope, limits, ref count, ref bytes)
            || !PortableSessionEntryJson.TryGuid(item, "agentId", out var agent)
            || !PortableSessionEntryJson.TryOptionalGuid(item, "sessionId", out var session)
            || !PortableSessionEntryJson.TryString(item, "correlationRef", out var reference)
            || reference != "state.correlation" || agent != address.AgentId.Value || session != address.SessionId.Value)
        {
            return false;
        }

        scope = new SecurityAuthorizationScope(new AgentId(agent), new SessionId(session!.Value), correlation);
        return true;
    }

    private static bool TryOptionalTimestamp(JsonElement parent, string name, out DateTimeOffset? value)
    {
        value = null;
        return parent.TryGetProperty(name, out var item) && (item.ValueKind == JsonValueKind.Null || (PortableSessionEntryJson.TryTimestamp(parent, name, out var parsed) && Assign(parsed, out value)));
    }

    private static string Subject(ExecutionSubjectKind value) => value switch { ExecutionSubjectKind.Human => "human", ExecutionSubjectKind.Service => "service", ExecutionSubjectKind.Workload => "workload", ExecutionSubjectKind.Anonymous => "anonymous", _ => throw new ArgumentOutOfRangeException(nameof(value)) };
    private static bool TrySubject(string value, out ExecutionSubjectKind result)
    {
        result = value switch
        {
            "human" => ExecutionSubjectKind.Human,
            "service" => ExecutionSubjectKind.Service,
            "workload" => ExecutionSubjectKind.Workload,
            "anonymous" => ExecutionSubjectKind.Anonymous,
            _ => (ExecutionSubjectKind) (-1),
        };
        return result >= 0;
    }
    private static string ClaimKind(IdentityClaimValueKind value) => value switch { IdentityClaimValueKind.Text => "text", IdentityClaimValueKind.Boolean => "boolean", IdentityClaimValueKind.WholeNumber => "wholeNumber", _ => throw new ArgumentOutOfRangeException(nameof(value)) };
    private static bool TryClaimKind(string value, out IdentityClaimValueKind result)
    {
        result = value switch
        {
            "text" => IdentityClaimValueKind.Text,
            "boolean" => IdentityClaimValueKind.Boolean,
            "wholeNumber" => IdentityClaimValueKind.WholeNumber,
            _ => (IdentityClaimValueKind) (-1),
        };
        return result >= 0;
    }
    private static string Assurance(IdentityAssuranceLevel value) => value switch { IdentityAssuranceLevel.Anonymous => "anonymous", IdentityAssuranceLevel.Basic => "basic", IdentityAssuranceLevel.Strong => "strong", IdentityAssuranceLevel.HardwareBacked => "hardwareBacked", _ => throw new ArgumentOutOfRangeException(nameof(value)) };
    private static bool TryAssurance(string value, out IdentityAssuranceLevel result)
    {
        result = value switch
        {
            "anonymous" => IdentityAssuranceLevel.Anonymous,
            "basic" => IdentityAssuranceLevel.Basic,
            "strong" => IdentityAssuranceLevel.Strong,
            "hardwareBacked" => IdentityAssuranceLevel.HardwareBacked,
            _ => (IdentityAssuranceLevel) (-1),
        };
        return result >= 0;
    }

    private static bool Assign<T>(T source, out T? value) where T : struct
    {
        value = source;
        return true;
    }
    private static FrozenSet<string> Set(params string[] names) => names.ToFrozenSet(StringComparer.Ordinal);
}
