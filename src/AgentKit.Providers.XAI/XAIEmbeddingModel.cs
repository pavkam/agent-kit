// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.XAI;

using AgentKit.Providers.OpenAICompatible;

/// <summary>
/// The xAI embedding <see cref="IEmbeddingModel"/>, built entirely from the
/// shared <see cref="OpenAICompatibleEmbeddingModelBase"/> pipeline.
/// </summary>
public sealed class XAIEmbeddingModel: OpenAICompatibleEmbeddingModelBase
{
    /// <summary>Initializes a new instance of the <see cref="XAIEmbeddingModel"/> class.</summary>
    /// <param name="descriptor">The descriptor of the xAI embedding model this instance serves.</param>
    /// <param name="profile">The xAI compatibility profile.</param>
    /// <param name="translator">Translates provider-neutral requests into OpenAI-compatible request bodies.</param>
    /// <param name="responseParser">Parses OpenAI-compatible embeddings responses into normalized results.</param>
    /// <param name="credentials">Resolves the current xAI credential.</param>
    /// <param name="httpClient">The HTTP client used to send requests.</param>
    /// <param name="timeProvider">The clock used for deadline and credential-expiry evaluation.</param>
    /// <exception cref="ArgumentNullException">Any parameter is null.</exception>
    public XAIEmbeddingModel(
        EmbeddingModelDescriptor descriptor,
        OpenAICompatibilityProfile profile,
        IOpenAIEmbeddingRequestTranslator translator,
        IOpenAIEmbeddingResponseParser responseParser,
        IProviderCredentialSource credentials,
        HttpClient httpClient,
        TimeProvider timeProvider)
        : base(descriptor, profile, translator, responseParser, credentials, httpClient, timeProvider)
    {
    }
}
