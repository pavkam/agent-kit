// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Declares the stable wire identity, exact local entry type, readable schema
/// set, write schema, and finite parsing limits for one session-entry codec.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="EntryType"/> is composition-only metadata. A codec catalog uses
/// it solely for an exact runtime-type lookup while writing; it is never
/// serialized, resolved by reflection, or used as a wire identity. Durable
/// selection uses <see cref="TypeId"/> and exact schema equality instead.
/// </para>
/// <para>
/// The readable schema sequence is ordered declarative metadata. A version is
/// supported only when listed exactly; no ordinal relation between version
/// texts implies compatibility. The descriptor's equality and hash code
/// compare that sequence structurally in declared order.
/// </para>
/// </remarks>
public sealed record SessionEntryCodecDescriptor
{
    /// <summary>
    /// Initializes one immutable codec declaration.
    /// </summary>
    /// <param name="typeId">
    /// The nondefault stable wire identity for this entry family.
    /// </param>
    /// <param name="entryType">
    /// The exact, closed, non-abstract <see cref="SessionEntry"/> subtype
    /// used only for local writer dispatch.
    /// </param>
    /// <param name="writeVersion">
    /// The nondefault schema version the codec emits. It must also occur in
    /// <paramref name="readableVersions"/>.
    /// </param>
    /// <param name="readableVersions">
    /// The initialized, duplicate-free ordered schema versions the codec can
    /// interpret exactly.
    /// </param>
    /// <param name="limits">
    /// The non-null finite limits enforced before the codec parses a stored
    /// payload or compatible unknown fields.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="entryType"/> or <paramref name="limits"/> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="typeId"/> or <paramref name="writeVersion"/> is
    /// default, or <paramref name="readableVersions"/> contains a default
    /// schema version.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="entryType"/> is not a concrete closed
    /// <see cref="SessionEntry"/> subtype; <paramref name="readableVersions"/>
    /// is default, repeats a schema version, or omits
    /// <paramref name="writeVersion"/>.
    /// </exception>
    public SessionEntryCodecDescriptor(
        SessionEntryTypeId typeId,
        Type entryType,
        SchemaVersion writeVersion,
        ImmutableArray<SchemaVersion> readableVersions,
        SessionEntryCodecLimits limits)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(typeId, default);
        ArgumentException.ThrowIfNotConcreteClosedType(entryType);
        ArgumentException.ThrowIfNotAssignableTo(entryType, typeof(SessionEntry), nameof(entryType));
        ArgumentOutOfRangeException.ThrowIfEqual(writeVersion, default);
        ArgumentException.ThrowIfInvalidSessionEntryCodecReadableVersions(readableVersions, writeVersion);
        ArgumentNullException.ThrowIfNull(limits);

        TypeId = typeId;
        EntryType = entryType;
        WriteVersion = writeVersion;
        ReadableVersions = readableVersions;
        Limits = limits;
    }

    /// <summary>
    /// Gets the stable durable protocol identity for this codec's entry
    /// family.
    /// </summary>
    public SessionEntryTypeId TypeId { get; }

    /// <summary>
    /// Gets the exact concrete closed entry subtype selected for local writer
    /// dispatch.
    /// </summary>
    /// <value>
    /// A concrete closed subtype of <see cref="SessionEntry"/>. It is local
    /// registration metadata and is never persisted or activated by
    /// reflection.
    /// </value>
    public Type EntryType { get; }

    /// <summary>
    /// Gets the exact schema version this codec writes.
    /// </summary>
    /// <value>
    /// A nondefault version which is also present in
    /// <see cref="ReadableVersions"/>.
    /// </value>
    public SchemaVersion WriteVersion { get; }

    /// <summary>
    /// Gets the ordered, duplicate-free schema versions this codec explicitly
    /// understands.
    /// </summary>
    /// <value>
    /// An initialized immutable sequence containing <see cref="WriteVersion"/>.
    /// Its order participates in descriptor equality as declared composition
    /// evidence; it does not imply version ordering or compatibility.
    /// </value>
    public ImmutableArray<SchemaVersion> ReadableVersions { get; }

    /// <summary>
    /// Gets the finite limits enforced before this codec parses persisted
    /// bytes.
    /// </summary>
    public SessionEntryCodecLimits Limits { get; }

    /// <summary>
    /// Determines whether this descriptor declares the same wire identity,
    /// local writer type, schemas, limits, and ordered readable-version list
    /// as <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The descriptor to compare, or <see langword="null"/>.</param>
    /// <returns>
    /// <see langword="true"/> when all scalar members match and readable
    /// versions match pairwise in order; otherwise <see langword="false"/>.
    /// </returns>
    public bool Equals(SessionEntryCodecDescriptor? other) =>
        other is not null
        && TypeId == other.TypeId
        && EntryType == other.EntryType
        && WriteVersion == other.WriteVersion
        && ReadableVersions.SequenceEqual(other.ReadableVersions)
        && Equals(Limits, other.Limits);

    /// <summary>
    /// Returns a hash code consistent with
    /// <see cref="Equals(SessionEntryCodecDescriptor?)"/>.
    /// </summary>
    /// <returns>
    /// A hash derived from every scalar member and every readable schema
    /// version in declared order.
    /// </returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(TypeId);
        hash.Add(EntryType);
        hash.Add(WriteVersion);
        foreach (var version in ReadableVersions)
        {
            hash.Add(version);
        }

        hash.Add(Limits);
        return hash.ToHashCode();
    }
}
