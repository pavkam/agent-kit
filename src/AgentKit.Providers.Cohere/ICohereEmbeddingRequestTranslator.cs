// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Cohere;

/// <summary>
/// Translates a provider-neutral <see cref="EmbeddingModelRequest"/> into a
/// Cohere v2 <c>POST /v2/embed</c> request body.
/// </summary>
public interface ICohereEmbeddingRequestTranslator
{
    /// <summary>Translates <paramref name="request"/> into an embed request body.</summary>
    /// <param name="request">The provider-neutral request to translate.</param>
    /// <returns>The embed request body.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="NotSupportedException">
    /// <paramref name="request"/> uses an input kind, purpose, or encoding
    /// this translator does not support translating for the Cohere v2
    /// embed wire format, or omits a purpose entirely, which Cohere's
    /// required <c>input_type</c> field has no safe default for.
    /// </exception>
    public JsonObject Translate(EmbeddingModelRequest request);
}
