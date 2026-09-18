// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Storage.Json;

/// <summary>Describes the immutable identity, schema version, and encoding contract recorded for one JSON store root.</summary>
/// <remarks>
/// The manifest is the only document rewritten outside a record append. It is created once during initialization and then
/// treated as read-only evidence. <see cref="FormatFingerprint"/> binds the store to the exact effective
/// <see cref="JsonSerializerOptions"/> used to write its records, so a later composition that configures a
/// different encoding fails closed instead of decoding previously written evidence under incompatible rules.
/// </remarks>
public sealed record JsonStoreManifest
{
    /// <summary>Initializes manifest evidence for one exact store root.</summary>
    /// <param name="storeId">The nonempty persistent store identity that every later open must match.</param>
    /// <param name="storeKind">The nonblank stable discriminator naming the owning storage family.</param>
    /// <param name="schemaVersion">The positive record-layout version understood by the reading adapter.</param>
    /// <param name="formatFingerprint">The nonblank fingerprint of the effective JSON encoding contract.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="storeId"/> is empty or <paramref name="schemaVersion"/> is not positive.</exception>
    /// <exception cref="ArgumentException"><paramref name="storeKind"/> or <paramref name="formatFingerprint"/> is null or blank.</exception>
    public JsonStoreManifest(Guid storeId, string storeKind, int schemaVersion, string formatFingerprint)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(storeId, Guid.Empty);
        ArgumentException.ThrowIfNullOrWhiteSpace(storeKind);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(schemaVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(formatFingerprint);

        StoreId = storeId;
        StoreKind = storeKind;
        SchemaVersion = schemaVersion;
        FormatFingerprint = formatFingerprint;
    }

    /// <summary>Gets the persistent store identity.</summary>
    /// <value>The nonempty identity compared against host bootstrap configuration before every access.</value>
    public Guid StoreId { get; }

    /// <summary>Gets the owning storage-family discriminator.</summary>
    /// <value>A stable nonblank name such as <c>agentkit.permissions.grants</c> that prevents cross-family reuse of one root.</value>
    public string StoreKind { get; }

    /// <summary>Gets the record-layout version.</summary>
    /// <value>A positive version; an adapter rejects any value it does not explicitly understand.</value>
    public int SchemaVersion { get; }

    /// <summary>Gets the fingerprint of the encoding contract that produced this store's records.</summary>
    /// <value>
    /// A nonblank fingerprint derived from the effective serializer options. A mismatch on open is reported as corrupt or
    /// unsupported evidence rather than silently reinterpreted, because caller-supplied options can change property names,
    /// converters, and number handling.
    /// </value>
    public string FormatFingerprint { get; }
}
