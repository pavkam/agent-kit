// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Sqlite;

using System.Buffers.Binary;

/// <summary>Reads strict bounded version-one security evidence while rejecting unknown or excessive state.</summary>
internal ref struct SqliteBudgetSecurityCodecReader
{
    private const uint _magic = 0x414B5347;
    private const byte _version = 1;
    private readonly ReadOnlySpan<byte> _payload;
    private int _offset;

    /// <summary>Initializes a reader over one caller-bounded envelope.</summary><param name="payload">The complete persisted bytes.</param><param name="settings">The immutable collection bounds.</param><exception cref="ArgumentNullException"><paramref name="settings"/> is null.</exception>
    internal SqliteBudgetSecurityCodecReader(ReadOnlySpan<byte> payload, SqliteBudgetLedgerSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _payload = payload;
    }

    /// <summary>Consumes and validates the fixed magic, codec version, and envelope kind.</summary><param name="expectedKind">The sole supported kind for the caller.</param><exception cref="InvalidDataException">The header is unsupported or truncated.</exception>
    internal void ReadHeader(byte expectedKind)
    {
        if (ReadUInt32() != _magic || ReadByte() != _version || ReadByte() != expectedKind)
        {
            throw new InvalidDataException("Persisted security evidence uses an unsupported envelope.");
        }
    }

    /// <summary>Reads one RFC 4122 network-order GUID.</summary><returns>The decoded value.</returns><exception cref="InvalidDataException">The envelope is truncated.</exception>
    internal Guid ReadGuid() => SqliteBudgetSecurityCodec.DecodeGuid(ReadBytes(16));
    /// <summary>Reads one big-endian signed 32-bit integer.</summary><returns>The decoded value.</returns><exception cref="InvalidDataException">The envelope is truncated.</exception>
    internal int ReadInt32() => BinaryPrimitives.ReadInt32BigEndian(ReadBytes(sizeof(int)));
    /// <summary>Reads one big-endian signed 64-bit integer.</summary><returns>The decoded value.</returns><exception cref="InvalidDataException">The envelope is truncated.</exception>
    internal long ReadInt64() => BinaryPrimitives.ReadInt64BigEndian(ReadBytes(sizeof(long)));

    /// <summary>Reads exact local ticks and offset ticks.</summary><returns>The validated timestamp preserving its offset.</returns><exception cref="ArgumentException">The encoded timestamp is invalid.</exception>
    internal DateTimeOffset ReadDateTimeOffset()
    {
        var ticks = ReadInt64();
        var offsetTicks = ReadInt64();
        return new DateTimeOffset(ticks, TimeSpan.FromTicks(offsetTicks));
    }

    /// <summary>Reads exact bounded UTF-16 code units, including standalone surrogates.</summary><returns>The reconstructed string.</returns><exception cref="InvalidDataException">The encoded length is excessive or truncated.</exception>
    internal string ReadString()
    {
        var length = ReadBoundedCount(int.MaxValue, sizeof(char));
        if (length > (_payload.Length - _offset) / sizeof(char))
        {
            throw new InvalidDataException("Persisted string evidence is truncated.");
        }

        var chars = new char[length];
        for (var index = 0; index < chars.Length; index++)
        {
            chars[index] = (char) BinaryPrimitives.ReadUInt16BigEndian(ReadBytes(sizeof(char)));
        }
        return new string(chars);
    }

    /// <summary>Reads one complete supported authorization scope.</summary><returns>The validated scope and concrete correlation leaf.</returns><exception cref="InvalidDataException">The correlation kind is unsupported.</exception>
    internal SecurityAuthorizationScope ReadScope()
    {
        var agentId = new AgentId(ReadGuid());
        SessionId? sessionId = ReadOptionalGuid() is { } session ? new SessionId(session) : null;
        OperationCorrelation correlation = ReadByte() switch
        {
            1 => new BeforeRunOperationCorrelation(
                new OperationId(ReadGuid()),
                ReadOptionalGuid() is { } admission ? new AdmissionId(admission) : null),
            2 => new InRunOperationCorrelation(
                new OperationId(ReadGuid()),
                new RunId(ReadGuid()),
                ReadOptionalGuid() is { } turn ? new TurnId(turn) : null),
            3 => new AfterRunOperationCorrelation(new OperationId(ReadGuid()), new RunId(ReadGuid())),
            _ => throw new InvalidDataException("Persisted operation correlation kind is unsupported."),
        };
        return new SecurityAuthorizationScope(agentId, sessionId, correlation);
    }

    /// <summary>Reads complete authenticated identity, claims, and delegation evidence.</summary><returns>The validated immutable identity.</returns><exception cref="InvalidDataException">A collection is excessive or the envelope is truncated.</exception>
    internal ExecutionIdentity ReadIdentity()
    {
        var tenantId = new TenantId(ReadString());
        var principalId = new PrincipalId(ReadString());
        var subjectKind = (ExecutionSubjectKind) ReadInt32();
        var evidence = ReadAuthenticationEvidence();
        var claims = ReadClaims();
        var delegationCount = ReadBoundedCount(int.MaxValue, 64);
        var delegation = ImmutableArray.CreateBuilder<DelegationIdentityLink>(delegationCount);
        for (var index = 0; index < delegationCount; index++)
        {
            delegation.Add(new DelegationIdentityLink(
                new DelegationId(ReadGuid()),
                new TenantId(ReadString()),
                new PrincipalId(ReadString()),
                new IdentityIssuerId(ReadString()),
                new AuthenticationEvidenceId(ReadString()),
                new IdentityVersion(ReadInt64()),
                ReadDateTimeOffset(),
                ReadClaims(),
                (IdentityAssuranceLevel) ReadInt32()));
        }

        return new ExecutionIdentity(
            tenantId,
            principalId,
            subjectKind,
            evidence,
            claims,
            delegation.MoveToImmutable(),
            (IdentityAssuranceLevel) ReadInt32(),
            new IdentityVersion(ReadInt64()));
    }

    /// <summary>Reads optional complete captured authorization evidence.</summary><returns>The validated context, or null for a legacy unpinned request.</returns>
    internal SecurityAuthorizationContext? ReadAuthorization() => ReadBoolean()
        ? new SecurityAuthorizationContext(
            new SecurityProfileKey(ReadString()),
            new SecurityProfileVersion(ReadInt64()),
            new SecurityPolicySnapshotReference(
                new SecurityPolicySnapshotId(ReadGuid()),
                new SecurityPolicyVersion(ReadInt64()),
                new ContentHash(ReadString())),
            new ComponentKey<ISecurityAuthority>(ReadString()),
            new AgentDefinitionRevision(ReadInt64()),
            new ConfigurationVersion(ReadInt64()),
            ReadScope(),
            ReadIdentity())
        : null;

    /// <summary>Reads nonempty ordered protected-resource evidence.</summary><returns>The immutable resources in persisted order.</returns><exception cref="InvalidDataException">The count is empty, excessive, or truncated.</exception>
    internal ImmutableArray<ProtectedResource> ReadResources()
    {
        var count = ReadBoundedCount(int.MaxValue, 8);
        if (count == 0)
        {
            throw new InvalidDataException("Persisted protected resources are empty.");
        }

        var resources = ImmutableArray.CreateBuilder<ProtectedResource>(count);
        for (var index = 0; index < count; index++)
        {
            resources.Add(new ProtectedResource((ProtectedResourceKind) ReadInt32(), ReadString()));
        }
        return resources.MoveToImmutable();
    }

    /// <summary>Rejects unknown trailing evidence after the expected envelope shape.</summary><exception cref="InvalidDataException">Unconsumed bytes remain.</exception>
    internal readonly void EnsureComplete()
    {
        _ = _offset == _payload.Length
            ? true
            : throw new InvalidDataException("Persisted security evidence contains unknown trailing fields.");
    }

    private AuthenticationEvidence ReadAuthenticationEvidence()
    {
        var id = new AuthenticationEvidenceId(ReadString());
        var issuer = new IdentityIssuerId(ReadString());
        var method = ReadString();
        var authenticatedAt = ReadDateTimeOffset();
        DateTimeOffset? expiresAt = ReadBoolean() ? ReadDateTimeOffset() : null;
        var fingerprint = new AuthenticationEvidenceFingerprint(new ContentHash(ReadString()));
        return new AuthenticationEvidence(id, issuer, method, authenticatedAt, expiresAt, fingerprint);
    }

    private ImmutableArray<IdentityClaim> ReadClaims()
    {
        var count = ReadBoundedCount(int.MaxValue, 16);
        var claims = ImmutableArray.CreateBuilder<IdentityClaim>(count);
        for (var index = 0; index < count; index++)
        {
            claims.Add(new IdentityClaim(
                new IdentityIssuerId(ReadString()),
                ReadString(),
                ReadString(),
                (IdentityClaimValueKind) ReadInt32()));
        }
        return claims.MoveToImmutable();
    }

    private Guid? ReadOptionalGuid() => ReadBoolean() ? ReadGuid() : null;

    private bool ReadBoolean() => ReadByte() switch
    {
        0 => false,
        1 => true,
        _ => throw new InvalidDataException("Persisted boolean evidence is invalid."),
    };

    private int ReadBoundedCount(int maximum, int minimumBytesPerElement = 1)
    {
        var count = ReadInt32();
        return count is >= 0
            && count <= maximum
            && count <= (_payload.Length - _offset) / minimumBytesPerElement
            ? count
            : throw new InvalidDataException(
                "Persisted collection evidence exceeds its configured bound.");
    }

    private byte ReadByte() => ReadBytes(1)[0];
    private uint ReadUInt32() => BinaryPrimitives.ReadUInt32BigEndian(ReadBytes(sizeof(uint)));

    private ReadOnlySpan<byte> ReadBytes(int length)
    {
        _ = length >= 0 && _offset <= _payload.Length - length
            ? true
            : throw new InvalidDataException("Persisted security evidence is truncated.");
        var result = _payload.Slice(_offset, length);
        _offset += length;
        return result;
    }
}
