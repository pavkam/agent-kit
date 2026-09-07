// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAI;

using AgentKit.Providers.OpenAICompatible;

/// <summary>
/// The fixed identity, endpoint, and capability defaults for the OpenAI
/// integration.
/// </summary>
public static class OpenAIProviderDefaults
{
    /// <summary>Gets the stable <see cref="ProviderId"/> for OpenAI.</summary>
    public static ProviderId ProviderId { get; } = new("openai");

    /// <summary>Gets the stable <see cref="ApiFamilyId"/> for the OpenAI Chat Completions wire format.</summary>
    public static ApiFamilyId ApiFamily { get; } = new("openai-chat-completions");

    /// <summary>Gets the stable <see cref="ApiFamilyId"/> for the OpenAI embeddings wire format.</summary>
    public static ApiFamilyId EmbeddingApiFamily { get; } = new("openai-embeddings");

    /// <summary>Gets OpenAI's public REST API base address.</summary>
    public static Uri DefaultBaseAddress { get; } = new("https://api.openai.com/");

    /// <summary>Gets the default chat completions operation path.</summary>
    public const string DefaultChatCompletionsPath = "v1/chat/completions";

    /// <summary>Gets the default embeddings operation path.</summary>
    public const string DefaultEmbeddingsPath = "v1/embeddings";

    /// <summary>
    /// Gets the default capability set applied to a registered OpenAI chat
    /// model unless the caller supplies its own.
    /// </summary>
    /// <remarks>
    /// These defaults describe a typical current-generation OpenAI chat
    /// model. A caller registering a model with materially different
    /// capabilities (for example, a reasoning-only model that does not
    /// support streaming) supplies its own <see cref="ModelCapabilities"/>
    /// rather than relying on this shared default.
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
    /// OpenAI chat model unless the caller supplies its own.
    /// </summary>
    public static ModelLimits DefaultLimits { get; } = new(maxContextTokens: null, maxOutputTokens: null);

    /// <summary>
    /// Gets the default capability set applied to a registered OpenAI
    /// embedding model unless the caller supplies its own.
    /// </summary>
    /// <remarks>
    /// These defaults describe OpenAI's current <c>text-embedding-3-*</c>
    /// family: batched text input, a caller-selectable reduced output
    /// dimensionality, and a choice of <c>float</c>/<c>base64</c> wire
    /// encoding. OpenAI has no task-type/purpose parameter and no explicit
    /// truncation-policy control.
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
    /// OpenAI embedding model unless the caller supplies its own.
    /// </summary>
    public static EmbeddingLimits DefaultEmbeddingLimits { get; } =
        new(maxInputsPerRequest: null, maxInputTokensPerInput: null, defaultDimensions: null, maxDimensions: null);

    /// <summary>
    /// Creates the <see cref="OpenAICompatibilityProfile"/> for the current
    /// <paramref name="options"/>.
    /// </summary>
    /// <param name="options">The validated OpenAI provider options.</param>
    /// <returns>A compatibility profile configured for OpenAI's public REST API.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    public static OpenAICompatibilityProfile CreateProfile(OpenAIProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new OpenAICompatibilityProfile(
            options.BaseAddress,
            options.ChatCompletionsPath,
            sendDeveloperRoleAsSystem: false,
            preferStreaming: options.PreferStreaming,
            includeStreamUsage: options.IncludeStreamUsage,
            useMaxCompletionTokensField: options.UseMaxCompletionTokensField,
            [],
            embeddingsPath: options.EmbeddingsPath);
    }
}
