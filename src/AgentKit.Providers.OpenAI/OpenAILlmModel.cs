// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAI;

using AgentKit.Providers.OpenAICompatible;

/// <summary>
/// The OpenAI conversational <see cref="ILlmModel"/>, built entirely from
/// the shared <see cref="OpenAICompatibleLlmModelBase"/> pipeline.
/// </summary>
public sealed class OpenAILlmModel: OpenAICompatibleLlmModelBase
{
    /// <summary>Initializes a new instance of the <see cref="OpenAILlmModel"/> class.</summary>
    /// <param name="descriptor">The descriptor of the OpenAI model this instance serves.</param>
    /// <param name="profile">The OpenAI compatibility profile.</param>
    /// <param name="translator">Translates provider-neutral requests into OpenAI-compatible request bodies.</param>
    /// <param name="streamParser">Parses OpenAI-compatible responses into normalized events.</param>
    /// <param name="credentials">Resolves the current OpenAI credential.</param>
    /// <param name="httpClient">The HTTP client used to send requests.</param>
    /// <param name="timeProvider">The clock used for deadline and credential-expiry evaluation.</param>
    /// <exception cref="ArgumentNullException">Any parameter is null.</exception>
    public OpenAILlmModel(
        ModelDescriptor descriptor,
        OpenAICompatibilityProfile profile,
        IOpenAIRequestTranslator translator,
        IOpenAIStreamParser streamParser,
        IProviderCredentialSource credentials,
        HttpClient httpClient,
        TimeProvider timeProvider)
        : base(
            descriptor,
            profile,
            translator,
            streamParser,
            credentials,
            httpClient,
            timeProvider)
    {
    }
}
