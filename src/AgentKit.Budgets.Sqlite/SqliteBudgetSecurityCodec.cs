// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Sqlite;

/// <summary>Encodes and reconstructs complete enforcement evidence using strict version-one binary envelopes.</summary>
/// <remarks>Strings are stored as exact big-endian UTF-16 code units, preserving unpaired surrogates. Unknown versions, kinds, trailing fields, invalid values, and exceeded bounds fail closed.</remarks>
internal static class SqliteBudgetSecurityCodec
{
    private const byte _enforcementKind = 2;

    /// <summary>Encodes one complete validated concrete enforcement request.</summary>
    /// <param name="enforcement">The non-null evidence to encode.</param>
    /// <param name="settings">The non-null configured evidence bounds.</param>
    /// <returns>The strict version-one enforcement envelope.</returns>
    /// <exception cref="ArgumentNullException">A parameter is null.</exception>
    /// <exception cref="ArgumentException">Evidence exceeds a configured bound or contains an unsupported correlation leaf.</exception>
    internal static byte[] EncodeEnforcement(
        SecurityEnforcementRequest enforcement,
        SqliteBudgetLedgerSettings settings)
    {
        ArgumentNullException.ThrowIfNull(enforcement);
        ArgumentNullException.ThrowIfNull(settings);
        var writer = new SqliteBudgetSecurityCodecWriter(settings, settings.MaximumPayloadBytes, nameof(enforcement));
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
        SqliteBudgetLedgerSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (payload.IsEmpty || payload.Length > settings.MaximumPayloadBytes)
        {
            throw new InvalidDataException("Persisted enforcement evidence has an invalid bounded length.");
        }

        try
        {
            var reader = new SqliteBudgetSecurityCodecReader(payload, settings);
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
