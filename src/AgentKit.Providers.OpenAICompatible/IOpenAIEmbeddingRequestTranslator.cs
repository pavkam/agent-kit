// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible;

/// <summary>
/// Translates a provider-neutral <see cref="EmbeddingModelRequest"/> into an
/// OpenAI-compatible <c>POST /embeddings</c> request body.
/// </summary>
public interface IOpenAIEmbeddingRequestTranslator
{
    /// <summary>Translates <paramref name="request"/> into an embeddings request body.</summary>
    /// <param name="request">The provider-neutral request to translate.</param>
    /// <param name="profile">The tested wire-behavior configuration for the target endpoint.</param>
    /// <returns>The embeddings request body.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="request"/> or <paramref name="profile"/> is null.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// <paramref name="request"/> uses an input kind, purpose, encoding, or
    /// truncation policy this translator does not support translating for
    /// the OpenAI-compatible embeddings wire format.
    /// </exception>
    public JsonObject Translate(EmbeddingModelRequest request, OpenAICompatibilityProfile profile);
}
