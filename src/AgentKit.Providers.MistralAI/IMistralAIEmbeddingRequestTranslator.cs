// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.MistralAI;

/// <summary>
/// Translates a provider-neutral <see cref="EmbeddingModelRequest"/> into a
/// Mistral AI <c>POST /v1/embeddings</c> request body.
/// </summary>
public interface IMistralAIEmbeddingRequestTranslator
{
    /// <summary>Translates <paramref name="request"/> into an embeddings request body.</summary>
    /// <param name="request">The provider-neutral request to translate.</param>
    /// <returns>The embeddings request body.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="NotSupportedException">
    /// <paramref name="request"/> uses an input kind, purpose, or
    /// truncation policy this translator does not support translating for
    /// the Mistral AI embeddings wire format.
    /// </exception>
    public JsonObject Translate(EmbeddingModelRequest request);
}
