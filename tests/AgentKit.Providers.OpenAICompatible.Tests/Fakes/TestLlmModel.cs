// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible.Tests.Fakes;

using AgentKit.Providers.Egress;

/// <summary>
/// The minimal concrete <see cref="OpenAICompatibleLlmModelBase"/>
/// subclass used to test the shared base pipeline directly, standing in
/// for a real branded provider package such as AgentKit.Providers.OpenAI.
/// </summary>
internal sealed class TestLlmModel: OpenAICompatibleLlmModelBase
{
    /// <summary>Initializes a new instance of the <see cref="TestLlmModel"/> class.</summary>
    /// <param name="descriptor">The descriptor of the model this instance serves.</param>
    /// <param name="profile">The tested wire-behavior configuration for the target endpoint.</param>
    /// <param name="translator">Translates provider-neutral requests into OpenAI-compatible request bodies.</param>
    /// <param name="streamParser">Parses OpenAI-compatible responses into normalized events.</param>
    /// <param name="credentials">Resolves the current credential for <paramref name="descriptor"/>'s provider.</param>
    /// <param name="egress">The provider-egress boundary every attempt sends through.</param>
    /// <param name="timeProvider">The clock used for deadline and credential-expiry evaluation.</param>
    public TestLlmModel(
        ModelDescriptor descriptor,
        OpenAICompatibilityProfile profile,
        IOpenAIRequestTranslator translator,
        IOpenAIStreamParser streamParser,
        IProviderCredentialSource credentials,
        ProviderEgress egress,
        TimeProvider timeProvider)
        : base(descriptor, profile, translator, streamParser, credentials, egress, timeProvider)
    {
    }
}
