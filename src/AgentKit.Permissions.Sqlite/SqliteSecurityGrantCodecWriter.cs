// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Sqlite;

using System.Buffers;
using System.Buffers.Binary;

/// <summary>Writes strict bounded version-one security evidence without growing beyond its configured limit.</summary>
internal sealed class SqliteSecurityGrantCodecWriter
{
    private const uint _magic = 0x414B5347;
    private const byte _grantEnvelopeVersionOne = 1;
    private const byte _grantEnvelopeVersionTwo = 2;
    private readonly ArrayBufferWriter<byte> _buffer = new();
    private readonly SqliteSecurityGrantStoreSettings _settings;
    private readonly int _maximumBytes;
    private readonly string _paramName;

    /// <summary>Initializes a writer with immutable evidence and aggregate byte bounds.</summary>
    /// <param name="settings">Collection bounds used while encoding nested evidence.</param><param name="maximumBytes">The positive envelope byte limit.</param><param name="paramName">The public evidence parameter reported when the limit is exceeded.</param>
    /// <exception cref="ArgumentNullException"><paramref name="settings"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maximumBytes"/> is not positive.</exception>
    /// <exception cref="ArgumentException"><paramref name="paramName"/> is null, empty, or whitespace.</exception>
    internal SqliteSecurityGrantCodecWriter(SqliteSecurityGrantStoreSettings settings, int maximumBytes, string paramName)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);
        ArgumentException.ThrowIfNullOrWhiteSpace(paramName);
        _settings = settings;
        _maximumBytes = maximumBytes;
        _paramName = paramName;
    }

    /// <summary>Writes the fixed magic, codec version, and exact envelope kind.</summary><param name="kind">The supported envelope discriminator.</param><param name="version">The supported envelope version.</param>
    internal void WriteHeader(byte kind, byte version)
    {
        WriteUInt32(_magic);
        WriteByte(version);
        WriteByte(kind);
    }

    /// <summary>Writes a single-byte boolean discriminator.</summary><param name="value">The value to encode.</param>
    internal void WriteBoolean(bool value) => WriteByte(value ? (byte) 1 : (byte) 0);

    /// <summary>Writes a GUID in RFC 4122 network byte order.</summary><param name="value">The value to encode.</param>
    internal void WriteGuid(Guid value)
    {
        EnsureRemaining(16);
        var span = _buffer.GetSpan(16);
        _ = value.TryWriteBytes(span, bigEndian: true, out var written);
        Debug.Assert(written == 16, "A GUID always writes sixteen bytes.");
        _buffer.Advance(16);
    }

    /// <summary>Writes a big-endian signed 32-bit integer.</summary><param name="value">The value to encode.</param>
    internal void WriteInt32(int value)
    {
        EnsureRemaining(sizeof(int));
        var span = _buffer.GetSpan(sizeof(int));
        BinaryPrimitives.WriteInt32BigEndian(span, value);
        _buffer.Advance(sizeof(int));
    }

    /// <summary>Writes a big-endian signed 64-bit integer.</summary><param name="value">The value to encode.</param>
    internal void WriteInt64(long value)
    {
        EnsureRemaining(sizeof(long));
        var span = _buffer.GetSpan(sizeof(long));
        BinaryPrimitives.WriteInt64BigEndian(span, value);
        _buffer.Advance(sizeof(long));
    }

    /// <summary>Writes exact local ticks and offset ticks without normalizing the represented instant.</summary><param name="value">The timestamp to encode.</param>
    internal void WriteDateTimeOffset(DateTimeOffset value)
    {
        WriteInt64(value.Ticks);
        WriteInt64(value.Offset.Ticks);
    }

    /// <summary>Writes the exact UTF-16 code units of a bounded non-null string.</summary><param name="value">The text to encode.</param><exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception><exception cref="ArgumentOutOfRangeException">The aggregate envelope bound would be exceeded.</exception>
    internal void WriteString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var encodedBytes = checked(value.Length * sizeof(char));
        EnsureRemaining(checked(sizeof(int) + encodedBytes));
        WriteInt32(value.Length);
        foreach (var codeUnit in value)
        {
            var span = _buffer.GetSpan(sizeof(char));
            BinaryPrimitives.WriteUInt16BigEndian(span, codeUnit);
            _buffer.Advance(sizeof(char));
        }
    }

    /// <summary>Writes a complete supported authorization scope and correlation leaf.</summary><param name="scope">The non-null scope to encode.</param><exception cref="ArgumentException">The correlation leaf is unsupported.</exception>
    internal void WriteScope(SecurityAuthorizationScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        WriteGuid(scope.AgentId.Value);
        WriteByte(scope.SessionId is null ? (byte) 0 : (byte) 1);
        if (scope.SessionId is { } sessionId)
        {
            WriteGuid(sessionId.Value);
        }

        switch (scope.Correlation)
        {
            case BeforeRunOperationCorrelation beforeRun:
                WriteByte(1);
                WriteGuid(beforeRun.OperationId.Value);
                WriteByte(beforeRun.AdmissionId is null ? (byte) 0 : (byte) 1);
                if (beforeRun.AdmissionId is { } admissionId)
                {
                    WriteGuid(admissionId.Value);
                }
                break;
            case InRunOperationCorrelation inRun:
                WriteByte(2);
                WriteGuid(inRun.OperationId.Value);
                WriteGuid(inRun.RunId.Value);
                WriteByte(inRun.TurnId is null ? (byte) 0 : (byte) 1);
                if (inRun.TurnId is { } turnId)
                {
                    WriteGuid(turnId.Value);
                }
                break;
            case AfterRunOperationCorrelation afterRun:
                WriteByte(3);
                WriteGuid(afterRun.OperationId.Value);
                WriteGuid(afterRun.CausalRunId.Value);
                break;
            default:
                throw new ArgumentException("The operation correlation kind is unsupported.", nameof(scope));
        }
    }

    /// <summary>Writes complete authenticated identity, claims, and delegation evidence.</summary><param name="identity">The non-null identity to encode.</param><exception cref="ArgumentOutOfRangeException">A configured collection bound is exceeded.</exception>
    internal void WriteIdentity(ExecutionIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(identity.Claims.Length, _settings.MaximumClaims, nameof(identity));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            identity.DelegationChain.Length, _settings.MaximumDelegationLinks, nameof(identity));
        WriteString(identity.TenantId.Value);
        WriteString(identity.PrincipalId.Value);
        WriteInt32((int) identity.SubjectKind);
        WriteAuthenticationEvidence(identity.Evidence);
        WriteClaims(identity.Claims);
        WriteInt32(identity.DelegationChain.Length);
        foreach (var link in identity.DelegationChain)
        {
            WriteGuid(link.Id.Value);
            WriteString(link.TenantId.Value);
            WriteString(link.PrincipalId.Value);
            WriteString(link.Issuer.Value);
            WriteString(link.EvidenceId.Value);
            WriteInt64(link.Version.Value);
            WriteDateTimeOffset(link.DelegatedAt);
            WriteClaims(link.Claims);
            WriteInt32((int) link.Assurance);
        }
        WriteInt32((int) identity.Assurance);
        WriteInt64(identity.Version.Value);
    }

    /// <summary>Writes optional complete captured authorization evidence.</summary><param name="authorization">The immutable context, or null for a legacy unpinned request.</param>
    internal void WriteAuthorization(SecurityAuthorizationContext? authorization)
    {
        WriteByte(authorization is null ? (byte) 0 : (byte) 1);
        if (authorization is null)
        {
            return;
        }

        WriteString(authorization.ProfileKey.Value);
        WriteInt64(authorization.ProfileVersion.Value);
        WriteGuid(authorization.PolicySnapshot.Id.Value);
        WriteInt64(authorization.PolicySnapshot.Version.Value);
        WriteString(authorization.PolicySnapshot.Fingerprint.Value);
        WriteString(authorization.AuthorityKey.Value);
        WriteInt64(authorization.AgentDefinitionRevision.Value);
        WriteInt64(authorization.ConfigurationVersion.Value);
        WriteScope(authorization.Scope);
        WriteIdentity(authorization.Identity);
    }

    /// <summary>Writes nonempty ordered protected-resource evidence.</summary><param name="resources">The resources to encode in source order.</param><exception cref="ArgumentException">The array is default, empty, or contains null.</exception><exception cref="ArgumentOutOfRangeException">The configured resource bound is exceeded.</exception>
    internal void WriteResources(ImmutableArray<ProtectedResource> resources)
    {
        ArgumentException.ThrowIfDefaultOrEmpty(resources);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(resources.Length, _settings.MaximumResources);
        WriteInt32(resources.Length);
        foreach (var resource in resources)
        {
            ArgumentNullException.ThrowIfNull(resource);
            WriteInt32((int) resource.Kind);
            WriteString(resource.Identifier);
        }
    }

    /// <summary>Copies the completed envelope after every growth was constrained by the captured aggregate bound.</summary>
    /// <returns>The exact encoded envelope.</returns>
    internal byte[] ToArray() => _buffer.WrittenSpan.ToArray();

    private void WriteAuthenticationEvidence(AuthenticationEvidence evidence)
    {
        Debug.Assert(evidence is not null, "Validated authentication evidence is required.");
        WriteString(evidence.Id.Value);
        WriteString(evidence.Issuer.Value);
        WriteString(evidence.Method);
        WriteDateTimeOffset(evidence.AuthenticatedAt);
        WriteByte(evidence.ExpiresAt is null ? (byte) 0 : (byte) 1);
        if (evidence.ExpiresAt is { } expiresAt)
        {
            WriteDateTimeOffset(expiresAt);
        }
        WriteString(evidence.SafeFingerprint.Hash.Value);
    }

    private void WriteClaims(ImmutableArray<IdentityClaim> claims)
    {
        Debug.Assert(!claims.IsDefault, "Validated claims are required.");
        ArgumentException.ThrowIfContainsNull(claims);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(claims.Length, _settings.MaximumClaims);
        WriteInt32(claims.Length);
        foreach (var claim in claims)
        {
            WriteString(claim.Issuer.Value);
            WriteString(claim.Type);
            WriteString(claim.Value);
            WriteInt32((int) claim.ValueKind);
        }
    }

    private void WriteByte(byte value)
    {
        EnsureRemaining(1);
        _buffer.GetSpan(1)[0] = value;
        _buffer.Advance(1);
    }

    private void WriteUInt32(uint value)
    {
        EnsureRemaining(sizeof(uint));
        var span = _buffer.GetSpan(sizeof(uint));
        BinaryPrimitives.WriteUInt32BigEndian(span, value);
        _buffer.Advance(sizeof(uint));
    }

    private void EnsureRemaining(int additionalBytes)
    {
        Debug.Assert(additionalBytes >= 0, "A nonnegative write size is required.");
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            additionalBytes,
            _maximumBytes - _buffer.WrittenCount,
            _paramName);
        _ = _buffer.GetSpan(additionalBytes);
    }
}
