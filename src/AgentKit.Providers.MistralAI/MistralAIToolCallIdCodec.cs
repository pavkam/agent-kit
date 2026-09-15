// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.MistralAI;

using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

/// <summary>
/// Derives and validates the wire-level tool-call identifiers accepted by the
/// Mistral AI Chat Completions API. Mistral rejects any
/// <c>tool_calls[].id</c> or <c>tool_call_id</c> that is not exactly nine
/// ASCII letters or digits (<c>^[a-zA-Z0-9]{9}$</c>), so the canonical
/// <see cref="ToolCallId"/> GUID can never be sent verbatim and identifiers
/// minted by other providers are not usable Mistral identities either.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Encode"/> is a pure, deterministic projection: it hashes the
/// big-endian GUID bytes of the <see cref="ToolCallId"/> together with a
/// big-endian 32-bit <c>disambiguator</c> using SHA-256, keeps the leading
/// 53 bits of the digest, and renders that value as exactly nine base-62
/// digits (<c>0-9</c>, <c>A-Z</c>, <c>a-z</c>). 2<sup>53</sup> is smaller
/// than 62<sup>9</sup>, so the rendering always fits without truncation and
/// two distinct 53-bit values always render as distinct strings. The
/// mapping is one-way; the canonical identity is recovered from history, not
/// decoded from the wire value.
/// </para>
/// <para>
/// Because the projection is lossy, two different identities may collide
/// within one request. The translator owns collision handling: it allocates
/// wire identifiers once per request, tries successive disambiguators for
/// an identity whose preferred wire value is already taken, and uses the
/// resulting map for both the assistant <c>tool_calls[].id</c> and the tool
/// <c>tool_call_id</c> so correlation is preserved end to end.
/// </para>
/// <para>
/// The type is stateless and thread-safe.
/// </para>
/// </remarks>
internal static class MistralAIToolCallIdCodec
{
    /// <summary>
    /// The exact number of characters Mistral requires in a tool-call
    /// identifier.
    /// </summary>
    public const int WireLength = 9;

    /// <summary>
    /// The number of leading digest bits rendered into the wire identifier.
    /// 2<sup>53</sup> &lt; 62<sup>9</sup>, so every value fits in nine
    /// base-62 digits.
    /// </summary>
    private const int _significantBits = 53;

    private const string _alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    /// <summary>
    /// Determines whether <paramref name="value"/> already satisfies
    /// Mistral's wire constraint and can therefore be echoed back unchanged.
    /// </summary>
    /// <param name="value">The candidate identifier text, typically a preserved <see cref="ProviderToolCallId"/>.</param>
    /// <returns>
    /// <see langword="true"/> when <paramref name="value"/> is exactly
    /// <see cref="WireLength"/> ASCII letters or digits; otherwise
    /// <see langword="false"/>, including for <see langword="null"/>.
    /// </returns>
    public static bool IsWireId([NotNullWhen(true)] string? value)
    {
        if (value is not { Length: WireLength })
        {
            return false;
        }

        foreach (var character in value)
        {
            if (!char.IsAsciiLetterOrDigit(character))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Deterministically derives a nine-character Mistral wire identifier
    /// from a canonical <see cref="ToolCallId"/>.
    /// </summary>
    /// <param name="callId">The canonical, non-default tool-call identity to project.</param>
    /// <param name="disambiguator">
    /// A non-negative sequence number the caller increments when the value
    /// produced for a lower number is already used by a different identity
    /// in the same request. <c>0</c> is the preferred encoding.
    /// </param>
    /// <returns>
    /// Exactly <see cref="WireLength"/> characters drawn from
    /// <c>0-9</c>, <c>A-Z</c>, and <c>a-z</c>. The same
    /// (<paramref name="callId"/>, <paramref name="disambiguator"/>) pair
    /// always yields the same string on every platform and process.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="callId"/> is the uninitialized default value, or
    /// <paramref name="disambiguator"/> is negative.
    /// </exception>
    public static string Encode(ToolCallId callId, int disambiguator = 0)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(callId, default);
        ArgumentOutOfRangeException.ThrowIfNegative(disambiguator);

        Span<byte> input = stackalloc byte[16 + sizeof(int)];
        var written = callId.Value.TryWriteBytes(input, bigEndian: true, out _);
        Debug.Assert(written, "A 20-byte buffer always holds the 16 GUID bytes.");
        BinaryPrimitives.WriteInt32BigEndian(input[16..], disambiguator);

        Span<byte> digest = stackalloc byte[SHA256.HashSizeInBytes];
        var digestLength = SHA256.HashData(input, digest);
        Debug.Assert(digestLength == SHA256.HashSizeInBytes, "SHA-256 always produces a 32-byte digest.");

        var value = BinaryPrimitives.ReadUInt64BigEndian(digest) >> (64 - _significantBits);

        Span<char> characters = stackalloc char[WireLength];
        for (var index = WireLength - 1; index >= 0; index--)
        {
            characters[index] = _alphabet[(int) (value % (ulong) _alphabet.Length)];
            value /= (ulong) _alphabet.Length;
        }

        Debug.Assert(value == 0, "53 bits always fit within nine base-62 digits.");
        return new string(characters);
    }
}
