// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible.Tests.Fakes;

using AgentKit.Providers.Egress;

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
    /// <param name="egress">The provider-egress boundary every attempt sends through.</param>
    /// <param name="timeProvider">The clock used for deadline evaluation.</param>
    /// <param name="profileSelector">The profile runtime selector.</param>
    /// <param name="scheme">The authorization scheme the override reports.</param>
    /// <param name="adjust">The payload edit applied by the override, also receiving the base's exposed <c>Descriptor</c>.</param>
    public CustomizingLlmModel(
        ModelDescriptor descriptor,
        OpenAICompatibilityProfile profile,
        IOpenAIRequestTranslator translator,
        IOpenAIStreamParser streamParser,
        ProviderEgress egress,
        TimeProvider timeProvider,
        IProviderProfileRuntimeSelector profileSelector,
        ProviderAuthorizationScheme scheme,
        Action<JsonObject, LlmModelRequest, ModelDescriptor> adjust)
        : base(descriptor, profile, translator, streamParser, egress, timeProvider, profileSelector)
    {
        _scheme = scheme;
        _adjust = adjust;
    }

    /// <inheritdoc/>
    protected override ProviderAuthorizationScheme AuthorizationScheme => _scheme;

    /// <inheritdoc/>
    protected override void AdjustRequestPayload(JsonObject payload, LlmModelRequest request) => _adjust(payload, request, Descriptor);
}
