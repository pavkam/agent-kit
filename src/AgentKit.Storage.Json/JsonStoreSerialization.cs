// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Storage.Json;

using System.Text.Json.Serialization.Metadata;

/// <summary>Builds the canonical JSON contract for this leaf and enforces bounded, lossless encoding of persisted evidence.</summary>
/// <remarks>
/// The canonical contract is deliberately strict: unmapped members are rejected, comments and trailing commas are refused,
/// numbers must be real JSON numbers, and enumerations are written as stable names rather than ordinals so a reordered
/// enumeration cannot silently change a persisted meaning. Callers may replace any of this through
/// <see cref="JsonEncodingOptions.SerializerOptions"/>; the store then binds the resulting fingerprint to the root and
/// verifies round-trip fidelity before accepting the composition.
/// </remarks>
public static class JsonStoreSerialization
{
    /// <summary>Creates a fresh mutable canonical contract for persisted store evidence.</summary>
    /// <returns>A new options instance that callers may mutate before it is frozen at registration.</returns>
    /// <remarks>
    /// Each call returns an independent instance so one composition's mutations never affect another. The returned contract
    /// is the baseline the adapter is tested against; deviating from it remains supported but shifts format responsibility
    /// to the caller.
    /// </remarks>
    public static JsonSerializerOptions CreateCanonicalOptions() => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        NumberHandling = JsonNumberHandling.Strict,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        AllowTrailingCommas = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = false,
        MaxDepth = 64,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        Converters = { new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false) },
    };

    /// <summary>Encodes one value under a frozen contract and enforces a byte bound before the payload is used.</summary>
    /// <typeparam name="TValue">The persisted shape being encoded.</typeparam>
    /// <param name="value">The non-null value to encode.</param>
    /// <param name="options">The frozen effective contract.</param>
    /// <param name="maximumBytes">The positive inclusive maximum encoded length.</param>
    /// <returns>The exact encoded UTF-8 bytes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> or <paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maximumBytes"/> is not positive.</exception>
    /// <exception cref="InvalidDataException">The encoded payload exceeds <paramref name="maximumBytes"/>.</exception>
    /// <exception cref="JsonException">The configured contract cannot encode the value.</exception>
    public static byte[] Encode<TValue>(TValue value, JsonSerializerOptions options, int maximumBytes)
        where TValue : notnull
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);
        var payload = JsonSerializer.SerializeToUtf8Bytes(value, options);
        return payload.Length <= maximumBytes
            ? payload
            : throw new InvalidDataException("An encoded JSON store payload exceeds its configured byte bound.");
    }

    /// <summary>Decodes one bounded payload under a frozen contract.</summary>
    /// <typeparam name="TValue">The persisted shape being decoded.</typeparam>
    /// <param name="payload">The exact encoded UTF-8 bytes.</param>
    /// <param name="options">The frozen effective contract.</param>
    /// <returns>The decoded non-null value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    /// <exception cref="InvalidDataException">The payload decodes to a null value.</exception>
    /// <exception cref="JsonException">The payload is malformed or violates the configured contract.</exception>
    public static TValue Decode<TValue>(ReadOnlySpan<byte> payload, JsonSerializerOptions options)
        where TValue : notnull
    {
        ArgumentNullException.ThrowIfNull(options);
        return JsonSerializer.Deserialize<TValue>(payload, options)
            ?? throw new InvalidDataException("A persisted JSON store payload decoded to a null value.");
    }

    /// <summary>Verifies that a configured contract can reproduce one representative persisted shape exactly.</summary>
    /// <typeparam name="TValue">The persisted shape used as the fidelity probe.</typeparam>
    /// <param name="probe">The non-null representative value covering the shape's nullable, collection, and enumeration members.</param>
    /// <param name="options">The frozen effective contract.</param>
    /// <exception cref="ArgumentNullException"><paramref name="probe"/> or <paramref name="options"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The contract cannot encode the probe, cannot decode its own output, or produces an unequal value.</exception>
    /// <remarks>
    /// This runs once during initialization so an unusable encoding contract fails at composition rather than while writing
    /// authoritative security evidence. It proves round-trip equality for the probe only; it cannot prove fidelity for every
    /// value a caller-supplied converter might encounter.
    /// </remarks>
    public static void VerifyRoundTrip<TValue>(TValue probe, JsonSerializerOptions options)
        where TValue : notnull
    {
        ArgumentNullException.ThrowIfNull(probe);
        ArgumentNullException.ThrowIfNull(options);
        TValue decoded;
        try
        {
            decoded = JsonSerializer.Deserialize<TValue>(JsonSerializer.SerializeToUtf8Bytes(probe, options), options)
                ?? throw new InvalidOperationException(
                    "The configured JSON encoding contract decoded its own output to a null value.");
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            throw new InvalidOperationException(
                "The configured JSON encoding contract cannot round-trip this store's persisted evidence.", exception);
        }

        if (!EqualityComparer<TValue>.Default.Equals(probe, decoded))
        {
            throw new InvalidOperationException(
                "The configured JSON encoding contract does not reproduce this store's persisted evidence exactly.");
        }
    }
}
