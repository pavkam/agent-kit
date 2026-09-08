// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleVertexAI;

/// <summary>
/// Translates a provider-neutral <see cref="EmbeddingModelRequest"/> into a
/// Vertex AI generic <c>:predict</c> request body for a text-embedding
/// model.
/// </summary>
public interface IGoogleVertexAIEmbeddingRequestTranslator
{
    /// <summary>Translates <paramref name="request"/> into a predict request body.</summary>
    /// <param name="request">The provider-neutral request to translate.</param>
    /// <returns>The predict request body.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="NotSupportedException">
    /// <paramref name="request"/> uses an input kind, encoding, or
    /// truncation policy this translator does not support translating for
    /// the Vertex AI text-embedding wire format.
    /// </exception>
    public JsonObject Translate(EmbeddingModelRequest request);
}
