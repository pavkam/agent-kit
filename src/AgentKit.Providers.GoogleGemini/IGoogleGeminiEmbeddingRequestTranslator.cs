// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleGemini;

/// <summary>
/// Translates a provider-neutral <see cref="EmbeddingModelRequest"/> into a
/// Gemini <c>batchEmbedContents</c> request body.
/// </summary>
public interface IGoogleGeminiEmbeddingRequestTranslator
{
    /// <summary>Translates <paramref name="request"/> into a batchEmbedContents request body.</summary>
    /// <param name="request">The provider-neutral request to translate.</param>
    /// <returns>The batchEmbedContents request body.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="NotSupportedException">
    /// <paramref name="request"/> uses an input kind, encoding, or
    /// truncation policy this translator does not support translating for
    /// the Gemini embeddings wire format.
    /// </exception>
    public JsonObject Translate(EmbeddingModelRequest request);
}
