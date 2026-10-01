// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible.Tests.Fakes;

using AgentKit.Providers.Egress;

/// <summary>
/// The minimal concrete <see cref="OpenAICompatibleEmbeddingModelBase"/>
/// subclass used to test the shared base pipeline directly, standing in
/// for a real branded provider package such as AgentKit.Providers.OpenAI.
/// </summary>
internal sealed class TestEmbeddingModel: OpenAICompatibleEmbeddingModelBase
{
    /// <summary>Initializes a new instance of the <see cref="TestEmbeddingModel"/> class.</summary>
    /// <param name="descriptor">The descriptor of the model this instance serves.</param>
    /// <param name="profile">The tested wire-behavior configuration for the target endpoint.</param>
    /// <param name="translator">Translates provider-neutral requests into OpenAI-compatible request bodies.</param>
    /// <param name="responseParser">Parses OpenAI-compatible embeddings responses into normalized results.</param>
    /// <param name="egress">The provider-egress boundary every attempt sends through.</param>
    /// <param name="timeProvider">The clock used for deadline evaluation.</param>
    /// <param name="profileSelector">The profile runtime selector.</param>
    public TestEmbeddingModel(
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
