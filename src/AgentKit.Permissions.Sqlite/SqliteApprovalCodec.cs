// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Sqlite;

using AgentKit.Permissions.Json;
using AgentKit.Storage.Json;

/// <summary>Encodes and decodes approval requests and responses using the portable JSON approval contract.</summary>
internal static class SqliteApprovalCodec
{
    private static JsonEncodingSettings Encoding { get; } = JsonEncodingSettings.CreateDefault();

    /// <summary>Encodes one approval request for SQLite persistence.</summary>
    /// <param name="request">The request to encode.</param>
    /// <param name="settings">The bounded evidence settings.</param>
    /// <returns>The encoded payload bytes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> or <paramref name="settings"/> is null.</exception>
    internal static byte[] EncodeRequest(ApprovalRequest request, SqliteApprovalStoreSettings settings)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(settings);
        return JsonStoreSerialization.Encode(
            JsonApprovalRequest.FromDomain(request),
            Encoding.RecordOptions,
            settings.MaximumRecordBytes);
    }

    /// <summary>Decodes one persisted approval request.</summary>
    /// <param name="payload">The encoded payload.</param>
    /// <param name="settings">The bounded evidence settings.</param>
    /// <returns>The reconstructed request.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="settings"/> is null.</exception>
    internal static ApprovalRequest DecodeRequest(ReadOnlySpan<byte> payload, SqliteApprovalStoreSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(payload.Length, settings.MaximumRecordBytes);
        return JsonStoreSerialization.Decode<JsonApprovalRequest>(payload, Encoding.RecordOptions).ToDomain();
    }

    /// <summary>Encodes one terminal approval response for SQLite persistence.</summary>
    /// <param name="response">The response to encode.</param>
    /// <param name="settings">The bounded evidence settings.</param>
    /// <returns>The encoded payload bytes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="response"/> or <paramref name="settings"/> is null.</exception>
    internal static byte[] EncodeResponse(ApprovalResponse response, SqliteApprovalStoreSettings settings)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(settings);
        return JsonStoreSerialization.Encode(
            JsonApprovalResponse.FromDomain(response),
            Encoding.RecordOptions,
            settings.MaximumRecordBytes);
    }

    /// <summary>Decodes one persisted terminal approval response.</summary>
    /// <param name="payload">The encoded payload.</param>
    /// <param name="settings">The bounded evidence settings.</param>
    /// <returns>The reconstructed response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="settings"/> is null.</exception>
    internal static ApprovalResponse DecodeResponse(ReadOnlySpan<byte> payload, SqliteApprovalStoreSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(payload.Length, settings.MaximumRecordBytes);
        return JsonStoreSerialization.Decode<JsonApprovalResponse>(payload, Encoding.RecordOptions).ToDomain();
    }
}
