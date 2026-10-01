// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Ollama;

using AgentKit.Providers.Egress;
using AgentKit.Providers.OpenAICompatible;

/// <summary>
/// The Ollama embedding <see cref="IEmbeddingModel"/>, built entirely from
/// the shared <see cref="OpenAICompatibleEmbeddingModelBase"/> pipeline
/// against Ollama's OpenAI-compatible embeddings alias.
/// </summary>
public sealed class OllamaEmbeddingModel: OpenAICompatibleEmbeddingModelBase
{
    /// <summary>Initializes a new instance of the <see cref="OllamaEmbeddingModel"/> class.</summary>
    /// <param name="descriptor">The descriptor of the Ollama embedding model this instance serves.</param>
    /// <param name="profile">The Ollama compatibility profile.</param>
    /// <param name="translator">Translates provider-neutral requests into OpenAI-compatible request bodies.</param>
    /// <param name="responseParser">Parses OpenAI-compatible embeddings responses into normalized results.</param>
    /// <param name="egress">The provider-egress boundary every attempt sends through.</param>
    /// <param name="timeProvider">The clock used for deadline evaluation.</param>
    /// <param name="profileSelector">The engine-wide profile runtime selector that resolves the descriptor's captured endpoint and credential profile binding.</param>
    /// <exception cref="ArgumentNullException">Any parameter is null.</exception>
    public OllamaEmbeddingModel(
        EmbeddingModelDescriptor descriptor,
        OpenAICompatibilityProfile profile,
        IOpenAIEmbeddingRequestTranslator translator,
        IOpenAIEmbeddingResponseParser responseParser,
        ProviderEgress egress,
        TimeProvider timeProvider,
        IProviderProfileRuntimeSelector profileSelector)
        : base(descriptor, profile, translator, responseParser, egress, timeProvider, profileSelector)
    {
    }
}
