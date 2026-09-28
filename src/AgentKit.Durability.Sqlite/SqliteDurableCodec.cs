// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Sqlite;

/// <summary>Encodes and reconstructs durable operation state using the shared portable durable JSON contract.</summary>
/// <remarks>
/// The SQLite adapter stores one encoded projection per operation rather than a bespoke binary layout, so the SQLite and
/// JSON leaves persist byte-identical evidence shapes and answer the shared conformance suite from the same documents.
/// Every payload is bounded before it reaches the database and again before it is decoded.
/// </remarks>
internal static class SqliteDurableCodec
{
    private static JsonEncodingSettings Encoding { get; } = JsonEncodingSettings.CreateDefault();

    /// <summary>Verifies the canonical contract can reproduce durable evidence exactly.</summary>
    /// <exception cref="InvalidOperationException">The contract cannot encode, decode, or exactly reproduce the fidelity probe.</exception>
    /// <remarks>This runs during bootstrap initialization so an unusable contract fails before any operation is journaled.</remarks>
    internal static void VerifyRoundTrip() =>
        JsonStoreSerialization.VerifyRoundTrip(DurableJournalProbe.Create(), Encoding.RecordOptions);

    /// <summary>Encodes one operation's complete accumulated state for SQLite persistence.</summary>
    /// <param name="projection">The non-null projection to persist.</param>
    /// <param name="settings">The bounded evidence settings.</param>
    /// <returns>The encoded payload bytes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="projection"/> or <paramref name="settings"/> is null.</exception>
    /// <exception cref="InvalidDataException">The encoded projection exceeds the configured record bound.</exception>
    internal static byte[] EncodeProjection(
        DurableOperationProjection projection,
        SqliteDurableStoreSettings settings)
    {
        ArgumentNullException.ThrowIfNull(projection);
        ArgumentNullException.ThrowIfNull(settings);
        return JsonStoreSerialization.Encode(
            DurableOperationProjectionDocument.FromDomain(projection),
            Encoding.RecordOptions,
            settings.MaximumRecordBytes);
    }

    /// <summary>Decodes one persisted operation projection.</summary>
    /// <param name="payload">The encoded payload.</param>
    /// <param name="settings">The bounded evidence settings.</param>
    /// <returns>The reconstructed live projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="settings"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="payload"/> exceeds the configured record bound.</exception>
    /// <exception cref="JsonException">The payload is malformed or violates the canonical contract.</exception>
    internal static DurableOperationProjection DecodeProjection(
        ReadOnlySpan<byte> payload,
        SqliteDurableStoreSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(payload.Length, settings.MaximumRecordBytes);
        return JsonStoreSerialization
            .Decode<DurableOperationProjectionDocument>(payload, Encoding.RecordOptions)
            .ToDomain();
    }

    /// <summary>Writes a GUID in RFC 4122 network byte order for schema keys.</summary>
    /// <param name="value">The GUID to encode.</param>
    /// <returns>The exact sixteen-byte representation.</returns>
    /// <remarks>Big-endian ordering keeps persisted key bytes stable across architectures and independent of the runtime's in-memory GUID layout.</remarks>
    internal static byte[] EncodeGuid(Guid value)
    {
        var result = new byte[16];
        _ = value.TryWriteBytes(result, bigEndian: true, out _);
        return result;
    }

    /// <summary>Reconstructs a GUID from its exact sixteen-byte network-order representation.</summary>
    /// <param name="value">The persisted sixteen bytes.</param>
    /// <returns>The decoded GUID.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is not exactly sixteen bytes.</exception>
    internal static Guid DecodeGuid(ReadOnlySpan<byte> value)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(value.Length, 16);
        return new Guid(value, bigEndian: true);
    }
}
