// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible.Tests.Fakes;

using AgentKit.Providers.Http;

/// <summary>
/// A concrete <see cref="OpenAICompatibleLlmModelBase"/> subclass that
/// overrides both dialect hooks, standing in for a branded package whose
/// endpoint authenticates through a dedicated header and amends the
/// translated body before it is sent.
/// </summary>
internal sealed class CustomizingLlmModel: OpenAICompatibleLlmModelBase
{
    private readonly ProviderAuthorizationScheme _scheme;
    private readonly Action<JsonObject, LlmModelRequest, ModelDescriptor> _adjust;

    /// <summary>Initializes a new instance of the <see cref="CustomizingLlmModel"/> class.</summary>
    /// <param name="descriptor">The descriptor of the model this instance serves.</param>
    /// <param name="profile">The tested wire-behavior configuration for the target endpoint.</param>
    /// <param name="translator">Translates provider-neutral requests into OpenAI-compatible request bodies.</param>
    /// <param name="streamParser">Parses OpenAI-compatible responses into normalized events.</param>
    /// <param name="credentials">Resolves the current credential for <paramref name="descriptor"/>'s provider.</param>
    /// <param name="httpClient">The HTTP client used to send requests.</param>
    /// <param name="timeProvider">The clock used for deadline and credential-expiry evaluation.</param>
    /// <param name="scheme">The authorization scheme the override reports.</param>
    /// <param name="adjust">The payload edit applied by the override, also receiving the base's exposed <c>Descriptor</c>.</param>
    public CustomizingLlmModel(
        ModelDescriptor descriptor,
        OpenAICompatibilityProfile profile,
        IOpenAIRequestTranslator translator,
        IOpenAIStreamParser streamParser,
        IProviderCredentialSource credentials,
        HttpClient httpClient,
        TimeProvider timeProvider,
        ProviderAuthorizationScheme scheme,
        Action<JsonObject, LlmModelRequest, ModelDescriptor> adjust)
        : base(descriptor, profile, translator, streamParser, credentials, httpClient, timeProvider)
    {
        _scheme = scheme;
        _adjust = adjust;
    }

    /// <inheritdoc/>
    protected override ProviderAuthorizationScheme AuthorizationScheme => _scheme;

    /// <inheritdoc/>
    protected override void AdjustRequestPayload(JsonObject payload, LlmModelRequest request) => _adjust(payload, request, Descriptor);
}
