// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.ZAI;

using AgentKit.Providers.Egress;
using AgentKit.Providers.OpenAICompatible;

/// <summary>
/// The Z.ai conversational <see cref="ILlmModel"/>, built entirely from the
/// shared <see cref="OpenAICompatibleLlmModelBase"/> pipeline.
/// </summary>
public sealed class ZAILlmModel: OpenAICompatibleLlmModelBase
{
    /// <summary>Initializes a new instance of the <see cref="ZAILlmModel"/> class.</summary>
    /// <param name="descriptor">The descriptor of the Z.ai model this instance serves.</param>
    /// <param name="profile">The Z.ai compatibility profile.</param>
    /// <param name="translator">Translates provider-neutral requests into OpenAI-compatible request bodies.</param>
    /// <param name="streamParser">Parses OpenAI-compatible responses into normalized events.</param>
    /// <param name="credentials">Resolves the current Z.ai credential.</param>
    /// <param name="egress">The provider-egress boundary every attempt sends through.</param>
    /// <param name="timeProvider">The clock used for deadline and credential-expiry evaluation.</param>
    /// <param name="profileSelector">The optional profile runtime selector used when the descriptor carries a binding.</param>
    /// <exception cref="ArgumentNullException">Any parameter is null.</exception>
    public ZAILlmModel(
        ModelDescriptor descriptor,
        OpenAICompatibilityProfile profile,
        IOpenAIRequestTranslator translator,
        IOpenAIStreamParser streamParser,
        IProviderCredentialSource credentials,
        ProviderEgress egress,
        TimeProvider timeProvider,
        IProviderProfileRuntimeSelector? profileSelector = null)
        : base(descriptor, profile, translator, streamParser, credentials, egress, timeProvider, profileSelector)
    {
    }
}
