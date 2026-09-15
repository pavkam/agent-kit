// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AzureOpenAI;

using System.Net.Http;

using AgentKit.Providers.Http;

/// <summary>
/// The Azure OpenAI embedding <see cref="IEmbeddingModel"/>, built on the
/// shared <see cref="OpenAICompatibleEmbeddingModelBase"/> pipeline with two
/// Azure-specific refinements: the <c>api-key</c>/Entra bearer
/// authentication scheme and deployment-name model addressing.
/// </summary>
/// <remarks>
/// <para>
/// Azure's GA v1 dialect authenticates an
/// <see cref="ApiKeyProviderCredential"/> through a dedicated
/// <c>api-key</c> header and an <see cref="OAuthTokenProviderCredential"/>
/// (a Microsoft Entra token) through <c>Authorization: Bearer</c>, which
/// <see cref="AzureOpenAIProviderDefaults.AuthorizationScheme"/> describes
/// and this class selects through <see cref="AuthorizationScheme"/>.
/// </para>
/// <para>
/// Azure also addresses a model by Azure deployment name (via
/// <see cref="EmbeddingModelDescriptor.DeploymentId"/>) rather than the
/// underlying model identity (<see cref="EmbeddingModelDescriptor.ModelId"/>)
/// alone, so after translation this class overwrites the body's
/// <c>model</c> field with the deployment name whenever one is configured.
/// </para>
/// </remarks>
public sealed class AzureOpenAIEmbeddingModel: OpenAICompatibleEmbeddingModelBase
{
    /// <summary>Initializes a new instance of the <see cref="AzureOpenAIEmbeddingModel"/> class.</summary>
    /// <param name="descriptor">
    /// The descriptor of the model this instance serves; its
    /// <see cref="EmbeddingModelDescriptor.DeploymentId"/> supplies the
    /// Azure deployment name sent as the wire <c>model</c> field.
    /// </param>
    /// <param name="profile">The tested wire-behavior configuration for the target resource endpoint.</param>
    /// <param name="translator">Translates provider-neutral requests into OpenAI-compatible request bodies.</param>
    /// <param name="responseParser">Parses OpenAI-compatible embeddings responses into normalized results.</param>
    /// <param name="credentials">Resolves the current credential for <paramref name="descriptor"/>'s provider.</param>
    /// <param name="httpClient">The HTTP client used to send requests.</param>
    /// <param name="timeProvider">The clock used for deadline and credential-expiry evaluation.</param>
    /// <exception cref="ArgumentNullException">Any parameter is null.</exception>
    public AzureOpenAIEmbeddingModel(
        EmbeddingModelDescriptor descriptor,
        OpenAICompatibilityProfile profile,
        IOpenAIEmbeddingRequestTranslator translator,
        IOpenAIEmbeddingResponseParser responseParser,
        IProviderCredentialSource credentials,
        HttpClient httpClient,
        TimeProvider timeProvider)
        : base(
            descriptor,
            profile,
            translator,
            responseParser,
            credentials,
            httpClient,
            timeProvider)
    {
    }

    /// <summary>
    /// Gets the Azure OpenAI GA v1 authentication scheme: API keys are
    /// sent as <c>api-key: &lt;key&gt;</c> and Entra tokens as
    /// <c>Authorization: Bearer &lt;token&gt;</c>.
    /// </summary>
    /// <value>Always <see cref="AzureOpenAIProviderDefaults.AuthorizationScheme"/>.</value>
    protected override ProviderAuthorizationScheme AuthorizationScheme => AzureOpenAIProviderDefaults.AuthorizationScheme;

    /// <summary>
    /// Replaces the translated <c>model</c> field with the configured Azure
    /// deployment name, when <see cref="OpenAICompatibleEmbeddingModelBase.Descriptor"/>
    /// carries one; a descriptor without a deployment leaves the
    /// translator's value untouched.
    /// </summary>
    /// <param name="payload">The translated request body owned by the current attempt.</param>
    /// <param name="request">The request being sent; unused because the deployment is adapter-bound.</param>
    protected override void AdjustRequestPayload(JsonObject payload, EmbeddingModelRequest request)
    {
        ArgumentNullException.ThrowIfNull(payload);

        if (Descriptor.DeploymentId is { } deploymentId)
        {
            payload["model"] = deploymentId.Value;
        }
    }
}
