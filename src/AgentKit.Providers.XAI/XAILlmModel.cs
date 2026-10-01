// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.XAI;

using AgentKit.Providers.Egress;
using AgentKit.Providers.OpenAICompatible;

/// <summary>
/// The xAI conversational <see cref="ILlmModel"/>, built entirely
/// from the shared <see cref="OpenAICompatibleLlmModelBase"/> pipeline.
/// </summary>
public sealed class XAILlmModel: OpenAICompatibleLlmModelBase
{
    /// <summary>Initializes a new instance of the <see cref="XAILlmModel"/> class.</summary>
    /// <param name="descriptor">The descriptor of the xAI model this instance serves.</param>
    /// <param name="profile">The xAI compatibility profile.</param>
    /// <param name="translator">Translates provider-neutral requests into OpenAI-compatible request bodies.</param>
    /// <param name="streamParser">Parses OpenAI-compatible responses into normalized events.</param>
    /// <param name="egress">The provider-egress boundary every attempt sends through.</param>
    /// <param name="timeProvider">The clock used for deadline evaluation.</param>
    /// <param name="profileSelector">The engine-wide profile runtime selector that resolves the descriptor's captured endpoint and credential profile binding.</param>
    /// <exception cref="ArgumentNullException">Any parameter is null.</exception>
    public XAILlmModel(
        ModelDescriptor descriptor,
        OpenAICompatibilityProfile profile,
        IOpenAIRequestTranslator translator,
        IOpenAIStreamParser streamParser,
        ProviderEgress egress,
        TimeProvider timeProvider,
        IProviderProfileRuntimeSelector profileSelector)
        : base(descriptor, profile, translator, streamParser, egress, timeProvider, profileSelector)
    {
    }
}
