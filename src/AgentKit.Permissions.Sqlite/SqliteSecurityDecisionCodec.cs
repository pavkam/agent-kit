// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Sqlite;

using AgentKit.Permissions.Json;
using AgentKit.Storage.Json;

/// <summary>Encodes and decodes terminal security decisions using the portable JSON decision contract.</summary>
internal static class SqliteSecurityDecisionCodec
{
    private static JsonEncodingSettings Encoding { get; } = JsonEncodingSettings.CreateDefault();

    /// <summary>Encodes one terminal decision for SQLite persistence.</summary>
    /// <param name="decision">The decision to encode.</param>
    /// <param name="settings">The bounded evidence settings.</param>
    /// <returns>The encoded payload bytes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="decision"/> or <paramref name="settings"/> is null.</exception>
    internal static byte[] Encode(SecurityDecision decision, SqliteSecurityDecisionStoreSettings settings)
    {
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(settings);
        return JsonStoreSerialization.Encode(
            JsonSecurityDecision.FromDomain(decision),
            Encoding.RecordOptions,
            settings.MaximumDecisionBytes);
    }

    /// <summary>Decodes one persisted terminal decision.</summary>
    /// <param name="payload">The encoded payload.</param>
    /// <param name="settings">The bounded evidence settings.</param>
    /// <returns>The reconstructed decision.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="settings"/> is null.</exception>
    internal static SecurityDecision Decode(ReadOnlySpan<byte> payload, SqliteSecurityDecisionStoreSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(payload.Length, settings.MaximumDecisionBytes);
        return JsonStoreSerialization.Decode<JsonSecurityDecision>(payload, Encoding.RecordOptions).ToDomain();
    }
}
