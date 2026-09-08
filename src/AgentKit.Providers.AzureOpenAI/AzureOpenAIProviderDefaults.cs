// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AzureOpenAI;

/// <summary>
/// The fixed identity, path, and capability defaults for the Azure OpenAI
/// GA v1 Chat Completions integration.
/// </summary>
/// <remarks>
/// This package covers only the GA <c>/openai/v1/chat/completions</c>
/// dialect on an Azure OpenAI resource, which no longer requires a dated
/// <c>api-version</c> query parameter and addresses a model by Azure
/// deployment name in the <c>model</c> body field. The older
/// deployment-scoped, <c>api-version</c>-dated route
/// (<c>/openai/deployments/{deployment}/chat/completions?api-version=...</c>)
/// is a materially different dialect the Azure documentation explicitly
/// calls out as needing its own adapter, and is not covered here. This
/// package also covers the corresponding GA v1
/// <c>/openai/v1/embeddings</c> embeddings dialect. The Responses API,
/// Images, Audio, Realtime, Files, vector stores, batch, and fine-tuning
/// are separate contracts this package does not translate.
/// </remarks>
public static class AzureOpenAIProviderDefaults
{
    /// <summary>Gets the stable <see cref="ProviderId"/> for Azure OpenAI.</summary>
    public static ProviderId ProviderId { get; } = new("azure-openai");

    /// <summary>Gets the stable <see cref="ApiFamilyId"/> for the Azure OpenAI GA v1 Chat Completions wire format.</summary>
    public static ApiFamilyId ApiFamily { get; } = new("azure-openai-chat-completions");

    /// <summary>Gets the stable <see cref="ApiFamilyId"/> for the Azure OpenAI GA v1 embeddings wire format.</summary>
    public static ApiFamilyId EmbeddingApiFamily { get; } = new("azure-openai-embeddings");

    /// <summary>Gets the default GA v1 chat completions operation path, relative to the resource endpoint.</summary>
    public const string DefaultChatCompletionsPath = "openai/v1/chat/completions";

    /// <summary>Gets the default GA v1 embeddings operation path, relative to the resource endpoint.</summary>
    public const string DefaultEmbeddingsPath = "openai/v1/embeddings";

    /// <summary>
    /// Gets the default capability set applied to a registered Azure
    /// OpenAI chat model unless the caller supplies its own.
    /// </summary>
    /// <remarks>
    /// These defaults describe a typical current-generation OpenAI chat
    /// model hosted on Azure. A caller registering a deployment with
    /// materially different capabilities (for example, a reasoning-only
    /// deployment that does not support streaming, or one behind a
    /// content-filter policy that rejects tool calls) supplies its own
    /// <see cref="ModelCapabilities"/> rather than relying on this shared
    /// default; capability and content-filter availability vary by
    /// deployment, region, and subscription.
    /// </remarks>
    public static ModelCapabilities DefaultCapabilities { get; } = new(
        supportsSystemInstructions: true,
        supportsStreaming: true,
        supportsToolCalls: true,
        supportsParallelToolCalls: true,
        supportsStructuredOutput: true,
        supportsReasoning: false,
        supportsVisionInput: false,
        ExtensionData.Empty);

    /// <summary>
    /// Gets the default, unbounded model limits applied to a registered
    /// Azure OpenAI chat model unless the caller supplies its own.
    /// </summary>
    public static ModelLimits DefaultLimits { get; } = new(maxContextTokens: null, maxOutputTokens: null);

    /// <summary>
    /// Gets the default capability set applied to a registered Azure
    /// OpenAI embedding model unless the caller supplies its own.
    /// </summary>
    /// <remarks>
    /// These defaults mirror plain OpenAI's <c>text-embedding-3-*</c>
    /// family capabilities. Capability and content-filter availability
    /// vary by deployment, region, and subscription; a caller registering
    /// a deployment with materially different capabilities supplies its
    /// own <see cref="EmbeddingCapabilities"/> rather than relying on this
    /// shared default.
    /// </remarks>
    public static EmbeddingCapabilities DefaultEmbeddingCapabilities { get; } = new(
        supportsBatchInput: true,
        supportsDimensions: true,
        supportsPurpose: false,
        supportsEncodingSelection: true,
        supportsTruncationControl: false,
        ExtensionData.Empty);

    /// <summary>
    /// Gets the default, unbounded embedding limits applied to a registered
    /// Azure OpenAI embedding model unless the caller supplies its own.
    /// </summary>
    public static EmbeddingLimits DefaultEmbeddingLimits { get; } =
        new(maxInputsPerRequest: null, maxInputTokensPerInput: null, defaultDimensions: null, maxDimensions: null);

    /// <summary>
    /// Creates the <see cref="OpenAICompatibilityProfile"/> for the current
    /// <paramref name="options"/>, reusing the OpenAI-compatible wire-shape
    /// translator and parser for the request/response body while this
    /// package supplies Azure's own authentication and endpoint
    /// construction.
    /// </summary>
    /// <param name="options">The validated Azure OpenAI provider options.</param>
    /// <returns>A compatibility profile configured for the caller's Azure OpenAI resource.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="options"/> or its <see cref="AzureOpenAIProviderOptions.ResourceEndpoint"/> is null.
    /// </exception>
    public static OpenAICompatibilityProfile CreateProfile(AzureOpenAIProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.ResourceEndpoint);

        return new OpenAICompatibilityProfile(
            options.ResourceEndpoint,
            options.ChatCompletionsPath,
            sendDeveloperRoleAsSystem: false,
            preferStreaming: options.PreferStreaming,
            includeStreamUsage: options.IncludeStreamUsage,
            useMaxCompletionTokensField: options.UseMaxCompletionTokensField,
            [],
            embeddingsPath: options.EmbeddingsPath);
    }
}
