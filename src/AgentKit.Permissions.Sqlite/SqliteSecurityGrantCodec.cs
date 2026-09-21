// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Sqlite;

/// <summary>Encodes and reconstructs complete grant and enforcement evidence using strict version-one binary envelopes.</summary>
/// <remarks>Strings are stored as exact big-endian UTF-16 code units, preserving unpaired surrogates. Unknown versions, kinds, trailing fields, invalid values, and exceeded bounds fail closed.</remarks>
internal static class SqliteSecurityGrantCodec
{
    private const byte _grantKind = 1;
    private const byte _enforcementKind = 2;
    private const byte _grantEnvelopeVersionOne = 1;
    private const byte _grantEnvelopeVersionTwo = 2;

    /// <summary>Encodes one complete validated immutable grant.</summary>
    /// <param name="grant">The non-null evidence to encode.</param>
    /// <param name="settings">The non-null configured evidence bounds.</param>
    /// <returns>The strict version-one grant envelope.</returns>
    /// <exception cref="ArgumentNullException">A parameter is null.</exception>
    /// <exception cref="ArgumentException">Evidence exceeds a configured bound or contains an unsupported correlation leaf.</exception>
    internal static byte[] EncodeGrant(SecurityGrant grant, SqliteSecurityGrantStoreSettings settings)
    {
        ArgumentNullException.ThrowIfNull(grant);
        ArgumentNullException.ThrowIfNull(settings);
        var writer = new SqliteSecurityGrantCodecWriter(settings, settings.MaximumGrantBytes, nameof(grant));
        writer.WriteHeader(_grantKind, _grantEnvelopeVersionTwo);
        writer.WriteGuid(grant.Id.Value);
        writer.WriteGuid(grant.RequestId.Value);
        writer.WriteScope(grant.Scope);
        writer.WriteIdentity(grant.Identity);
        writer.WriteAuthorization(grant.Authorization);
        writer.WriteString(grant.Audience.Value);
        writer.WriteInt32((int) grant.Kind);
        writer.WriteInt32((int) grant.Effect);
        writer.WriteResources(grant.Resources);
        writer.WriteString(grant.InputFingerprint.Value);
        writer.WriteInt64(grant.PolicyVersion.Value);
        writer.WriteInt64(grant.RevocationVersion.Value);
        writer.WriteDateTimeOffset(grant.NotBefore);
        writer.WriteDateTimeOffset(grant.ExpiresAt);
        writer.WriteInt32(grant.AllowedUses);
        if (grant.Approval is { } approval)
        {
            writer.WriteBoolean(true);
            writer.WriteGuid(approval.Value);
        }
        else
        {
            writer.WriteBoolean(false);
        }

        return writer.ToArray();
    }

    /// <summary>Reconstructs one complete immutable grant from strict persisted evidence.</summary>
    /// <param name="payload">The nonempty version-one envelope.</param>
    /// <param name="settings">The non-null configured evidence bounds.</param>
    /// <returns>The validated domain grant, preserving timestamp offsets exactly.</returns>
    /// <exception cref="InvalidDataException">The envelope is corrupt, unsupported, incomplete, excessive, or contains trailing evidence.</exception>
    internal static SecurityGrant DecodeGrant(ReadOnlySpan<byte> payload, SqliteSecurityGrantStoreSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (payload.IsEmpty || payload.Length > settings.MaximumGrantBytes)
        {
            throw new InvalidDataException("Persisted grant evidence has an invalid bounded length.");
        }

        try
        {
            var reader = new SqliteSecurityGrantCodecReader(payload, settings);
            var envelopeVersion = reader.ReadHeader(_grantKind);
            var id = new GrantId(reader.ReadGuid());
            var requestId = new SecurityRequestId(reader.ReadGuid());
            var scope = reader.ReadScope();
            var identity = reader.ReadIdentity();
            var authorization = reader.ReadAuthorization();
            var audience = new ComponentId(reader.ReadString());
            var kind = (SecurityOperationKind) reader.ReadInt32();
            var effect = (SecurityEffect) reader.ReadInt32();
            var resources = reader.ReadResources();
            var inputFingerprint = new InputFingerprint(reader.ReadString());
            var policyVersion = new SecurityPolicyVersion(reader.ReadInt64());
            var revocationVersion = new SecurityRevocationVersion(reader.ReadInt64());
            var notBefore = reader.ReadDateTimeOffset();
            var expiresAt = reader.ReadDateTimeOffset();
            var allowedUses = reader.ReadInt32();
            reader.EnsureComplete();
            return authorization is null
                ? new SecurityGrant(id, requestId, scope, identity, audience, kind, effect, resources,
                    inputFingerprint, policyVersion, revocationVersion, notBefore, expiresAt, allowedUses)
                : new SecurityGrant(id, requestId, scope, identity, authorization, audience, kind, effect, resources,
                    inputFingerprint, policyVersion, revocationVersion, notBefore, expiresAt, allowedUses);
        }
        catch (Exception exception) when (exception is ArgumentException or OverflowException)
        {
            throw new InvalidDataException("Persisted grant evidence is invalid.", exception);
        }
    }

    /// <summary>Encodes one complete validated concrete enforcement request.</summary>
    /// <param name="enforcement">The non-null evidence to encode.</param>
    /// <param name="settings">The non-null configured evidence bounds.</param>
    /// <returns>The strict version-one enforcement envelope.</returns>
    /// <exception cref="ArgumentNullException">A parameter is null.</exception>
    /// <exception cref="ArgumentException">Evidence exceeds a configured bound or contains an unsupported correlation leaf.</exception>
    internal static byte[] EncodeEnforcement(
        SecurityEnforcementRequest enforcement,
        SqliteSecurityGrantStoreSettings settings)
    {
        ArgumentNullException.ThrowIfNull(enforcement);
        ArgumentNullException.ThrowIfNull(settings);
        var writer = new SqliteSecurityGrantCodecWriter(settings, settings.MaximumEnforcementBytes, nameof(enforcement));
        writer.WriteHeader(_enforcementKind);
        writer.WriteScope(enforcement.Scope);
        writer.WriteIdentity(enforcement.Identity);
        writer.WriteAuthorization(enforcement.Authorization);
        writer.WriteString(enforcement.Audience.Value);
        writer.WriteInt32((int) enforcement.Kind);
        writer.WriteInt32((int) enforcement.Effect);
        writer.WriteResources(enforcement.Resources);
        writer.WriteString(enforcement.InputFingerprint.Value);
        writer.WriteInt64(enforcement.RevocationVersion.Value);
        return writer.ToArray();
    }

    /// <summary>Reconstructs one complete enforcement request from strict persisted evidence.</summary>
    /// <param name="payload">The nonempty version-one envelope.</param>
    /// <param name="settings">The non-null configured evidence bounds.</param>
    /// <returns>The validated domain evidence with ordered resources and captured authorization intact.</returns>
    /// <exception cref="InvalidDataException">The envelope is corrupt, unsupported, incomplete, excessive, or contains trailing evidence.</exception>
    internal static SecurityEnforcementRequest DecodeEnforcement(
        ReadOnlySpan<byte> payload,
        SqliteSecurityGrantStoreSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (payload.IsEmpty || payload.Length > settings.MaximumEnforcementBytes)
        {
            throw new InvalidDataException("Persisted enforcement evidence has an invalid bounded length.");
        }

        try
        {
            var reader = new SqliteSecurityGrantCodecReader(payload, settings);
            reader.ReadHeader(_enforcementKind);
            var scope = reader.ReadScope();
            var identity = reader.ReadIdentity();
            var authorization = reader.ReadAuthorization();
            var audience = new ComponentId(reader.ReadString());
            var kind = (SecurityOperationKind) reader.ReadInt32();
            var effect = (SecurityEffect) reader.ReadInt32();
            var resources = reader.ReadResources();
            var inputFingerprint = new InputFingerprint(reader.ReadString());
            var revocationVersion = new SecurityRevocationVersion(reader.ReadInt64());
            reader.EnsureComplete();
            return authorization is null
                ? new SecurityEnforcementRequest(scope, identity, audience, kind, effect, resources,
                    inputFingerprint, revocationVersion)
                : new SecurityEnforcementRequest(scope, identity, authorization, audience, kind, effect, resources,
                    inputFingerprint, revocationVersion);
        }
        catch (Exception exception) when (exception is ArgumentException or OverflowException)
        {
            throw new InvalidDataException("Persisted enforcement evidence is invalid.", exception);
        }
    }

    /// <summary>Writes a GUID in RFC 4122 network byte order for schema keys.</summary>
    /// <param name="value">The GUID to encode.</param>
    /// <returns>The exact sixteen-byte representation.</returns>
    internal static byte[] EncodeGuid(Guid value)
    {
        var result = new byte[16];
        _ = value.TryWriteBytes(result, bigEndian: true, out var bytesWritten);
        Debug.Assert(bytesWritten == result.Length, "A GUID always writes sixteen bytes.");
        return result;
    }

    /// <summary>Reads one RFC 4122 network-order GUID schema key.</summary>
    /// <param name="value">The exact sixteen-byte representation.</param>
    /// <returns>The reconstructed GUID.</returns>
    /// <exception cref="InvalidDataException"><paramref name="value"/> does not contain exactly sixteen bytes.</exception>
    internal static Guid DecodeGuid(ReadOnlySpan<byte> value) => value.Length == 16
        ? new Guid(value, bigEndian: true)
        : throw new InvalidDataException("Persisted identity evidence must contain sixteen bytes.");



}
