// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleGemini;

/// <summary>
/// Writes and reads the opaque Gemini <c>thoughtSignature</c> a
/// <c>functionCall</c> or <c>text</c> part carries, preserved under
/// <see cref="GoogleGeminiExtensionKeys.ThoughtSignature"/> on the part's
/// <see cref="ContentPart.Extensions"/>.
/// </summary>
/// <remarks>
/// <para>
/// Gemini's GenerateContent API has no dedicated thought block for
/// function-calling turns. The encrypted reasoning state is instead
/// attached to the <c>functionCall</c> part itself (and, for a
/// non-function-call answer, possibly to the last text part), and the
/// provider requires the signature to be returned inside that same part on
/// the next request; Gemini 3 models reject a request that omits a required
/// function-call signature. AgentKit keeps the signature as typed extension
/// data on the exact part that carried it so the request translator can
/// re-emit it in place without merging or reordering parts.
/// </para>
/// <para>
/// The signature is an opaque provider token. It is neither validated nor
/// interpreted here, and it must not be logged as content.
/// </para>
/// <para>
/// This type is stateless and safe to call concurrently.
/// </para>
/// </remarks>
public static class GoogleGeminiThoughtSignature
{
    /// <summary>
    /// Builds the extension data that retains <paramref name="signature"/>
    /// under <see cref="GoogleGeminiExtensionKeys.ThoughtSignature"/>, or
    /// <see cref="ExtensionData.Empty"/> when the part carried none.
    /// </summary>
    /// <param name="signature">
    /// The <c>thoughtSignature</c> exactly as the provider returned it, or
    /// <see langword="null"/> when the part had none.
    /// </param>
    /// <returns>
    /// Extension data holding exactly one entry under
    /// <see cref="GoogleGeminiExtensionKeys.ThoughtSignature"/>, or
    /// <see cref="ExtensionData.Empty"/> when <paramref name="signature"/>
    /// is <see langword="null"/> or empty.
    /// </returns>
    public static ExtensionData Create(string? signature) =>
        string.IsNullOrEmpty(signature)
            ? ExtensionData.Empty
            : new ExtensionData(
                ImmutableDictionary<string, ExtensionValue>.Empty.Add(
                    GoogleGeminiExtensionKeys.ThoughtSignature,
                    new ExtensionValue([.. JsonSerializer.SerializeToUtf8Bytes(signature)])));

    /// <summary>
    /// Reads the retained <c>thoughtSignature</c> from
    /// <paramref name="extensions"/>, when one is present.
    /// </summary>
    /// <param name="extensions">The part's extension data to inspect.</param>
    /// <returns>
    /// The signature; or <see langword="null"/> when
    /// <paramref name="extensions"/> has no entry under
    /// <see cref="GoogleGeminiExtensionKeys.ThoughtSignature"/>, or the
    /// entry is not a non-empty JSON string.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="extensions"/> is <see langword="null"/>.</exception>
    public static string? TryRead(ExtensionData extensions)
    {
        ArgumentNullException.ThrowIfNull(extensions);

        if (!extensions.Values.TryGetValue(GoogleGeminiExtensionKeys.ThoughtSignature, out var value))
        {
            return null;
        }

        var reader = new Utf8JsonReader(value.CanonicalJson.AsSpan());
        if (!reader.Read() || reader.TokenType != JsonTokenType.String)
        {
            return null;
        }

        var signature = reader.GetString();
        return string.IsNullOrEmpty(signature) ? null : signature;
    }
}
