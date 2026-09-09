// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Carries one provider-neutral session-entry payload selected by a
/// stable protocol identity and exact schema version.
/// </summary>
/// <remarks>
/// The payload is opaque at this boundary. A store persists it verbatim and a
/// codec selected by <see cref="TypeId"/> and <see cref="SchemaVersion"/>
/// interprets it without CLR type-name activation. Keeping the original bytes
/// with a successful decode permits the store to retain compatible fields that
/// the current model cannot materialize, including nested fields. Codec limits
/// are enforced by the selected codec before it parses these bytes.
/// </remarks>
public sealed record SessionEntryWireEnvelope
{
    /// <summary>Initializes one immutable durable entry envelope.</summary>
    /// <param name="typeId">The nondefault stable entry-family wire identity.</param>
    /// <param name="schemaVersion">The nondefault exact schema used for <paramref name="payload"/>.</param>
    /// <param name="payload">The initialized UTF-8 payload bytes owned by this envelope.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="typeId"/> or <paramref name="schemaVersion"/> is default.</exception>
    /// <exception cref="ArgumentException"><paramref name="payload"/> is default or empty.</exception>
    public SessionEntryWireEnvelope(SessionEntryTypeId typeId, SchemaVersion schemaVersion, ImmutableArray<byte> payload)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(typeId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(schemaVersion, default);
        ArgumentException.ThrowIfDefaultOrEmpty(payload);
        TypeId = typeId;
        SchemaVersion = schemaVersion;
        Payload = payload;
    }

    /// <summary>Gets the stable wire identity selecting a codec family.</summary>
    public SessionEntryTypeId TypeId { get; }

    /// <summary>Gets the exact schema version selecting a codec reader.</summary>
    public SchemaVersion SchemaVersion { get; }

    /// <summary>Gets the immutable original payload bytes.</summary>
    public ImmutableArray<byte> Payload { get; }

    /// <summary>Compares identity, schema, and payload bytes structurally.</summary>
    public bool Equals(SessionEntryWireEnvelope? other) =>
        other is not null
        && TypeId == other.TypeId
        && SchemaVersion == other.SchemaVersion
        && Payload.AsSpan().SequenceEqual(other.Payload.AsSpan());

    /// <summary>Returns a hash derived from identity, schema, and every payload byte.</summary>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(TypeId);
        hash.Add(SchemaVersion);
        hash.AddBytes(Payload.AsSpan());
        return hash.ToHashCode();
    }
}
