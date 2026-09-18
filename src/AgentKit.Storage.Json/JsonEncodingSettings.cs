// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Storage.Json;

/// <summary>Captures the immutable, fingerprinted JSON encoding contract bound to one initialized store root.</summary>
/// <remarks>
/// Construction freezes the supplied serializer options and derives a compact variant used for newline-delimited records.
/// Two settings values are equal when their fingerprints match, which is exactly the condition under which one store root's
/// records can be decoded by the other's contract.
/// </remarks>
public sealed record JsonEncodingSettings
{
    /// <summary>Freezes one effective encoding contract and derives its record and document writers.</summary>
    /// <param name="serializerOptions">The effective options; the instance is made read-only and retained.</param>
    /// <exception cref="ArgumentNullException"><paramref name="serializerOptions"/> is null.</exception>
    /// <remarks>
    /// The supplied instance is made read-only, so a caller that retains a reference cannot mutate the contract after
    /// composition. The derived record contract always writes compact output regardless of the configured indentation.
    /// </remarks>
    public JsonEncodingSettings(JsonSerializerOptions serializerOptions)
    {
        ArgumentNullException.ThrowIfNull(serializerOptions);
        serializerOptions.MakeReadOnly();
        DocumentOptions = serializerOptions;
        RecordOptions = serializerOptions.WriteIndented
            ? MakeCompact(serializerOptions)
            : serializerOptions;
        Fingerprint = JsonFormatFingerprint.Compute(serializerOptions);
    }

    /// <summary>Gets the read-only contract used when rewriting a whole document such as the store manifest.</summary>
    /// <value>The frozen caller-configured options, honoring indentation when requested.</value>
    public JsonSerializerOptions DocumentOptions { get; }

    /// <summary>Gets the read-only contract used for every newline-delimited record.</summary>
    /// <value>
    /// The same semantic contract as <see cref="DocumentOptions"/> with indentation suppressed, so each record occupies
    /// exactly one line. When indentation was not requested this is the identical instance.
    /// </value>
    public JsonSerializerOptions RecordOptions { get; }

    /// <summary>Gets the fingerprint of the semantic option set.</summary>
    /// <value>
    /// A lowercase hexadecimal SHA-256 fingerprint stored in the manifest and compared on every open. Presentation-only
    /// settings are excluded, so toggling indentation does not invalidate an existing store.
    /// </value>
    public string Fingerprint { get; }

    /// <summary>Compares two encoding contracts by their semantic fingerprint.</summary>
    /// <param name="other">The candidate contract.</param>
    /// <returns><see langword="true"/> when both contracts decode and encode records identically.</returns>
    public bool Equals(JsonEncodingSettings? other) =>
        other is not null && string.Equals(Fingerprint, other.Fingerprint, StringComparison.Ordinal);

    /// <summary>Returns a hash consistent with semantic fingerprint equality.</summary>
    /// <returns>The ordinal hash of <see cref="Fingerprint"/>.</returns>
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Fingerprint);

    /// <summary>Creates default canonical encoding settings.</summary>
    /// <returns>Settings built from <see cref="JsonStoreSerialization.CreateCanonicalOptions"/>.</returns>
    public static JsonEncodingSettings CreateDefault() => new(JsonStoreSerialization.CreateCanonicalOptions());

    private static JsonSerializerOptions MakeCompact(JsonSerializerOptions source)
    {
        Debug.Assert(source is not null, "A frozen source contract is required.");
        var compact = new JsonSerializerOptions(source) { WriteIndented = false };
        compact.MakeReadOnly();
        return compact;
    }
}
