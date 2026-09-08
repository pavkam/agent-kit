// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.MistralAI;

/// <summary>
/// The fixed identity, endpoint, and capability defaults for the Mistral AI
/// Chat Completions integration.
/// </summary>
/// <remarks>
/// This package covers <c>POST /v1/chat/completions</c> (buffered and
/// streaming) for text and client-executed function tools, and
/// <c>POST /v1/embeddings</c>. Fill-in-the-middle, Conversations/Agents,
/// moderation, classification, OCR, audio, files, libraries, batch, and
/// fine-tuning are separate contracts not covered by this package. Hosted
/// tools (web search, code interpreter, image generation, document
/// library, custom connectors) and the <c>response_format</c>
/// structured-output control are not yet translated.
/// </remarks>
public static class MistralAIProviderDefaults
{
    /// <summary>Gets the stable <see cref="ProviderId"/> for Mistral AI.</summary>
    public static ProviderId ProviderId { get; } = new("mistral-ai");

    /// <summary>Gets the stable <see cref="ApiFamilyId"/> for Mistral's Chat Completions wire format.</summary>
    public static ApiFamilyId ApiFamily { get; } = new("mistral-chat-completions");

    /// <summary>Gets the stable <see cref="ApiFamilyId"/> for Mistral's embeddings wire format.</summary>
    public static ApiFamilyId EmbeddingApiFamily { get; } = new("mistral-embeddings");

    /// <summary>Gets the Mistral AI API's base address.</summary>
    public static Uri DefaultBaseAddress { get; } = new("https://api.mistral.ai/v1/");

    /// <summary>Gets the default path, relative to <see cref="DefaultBaseAddress"/>, of the chat completions operation.</summary>
    public const string DefaultChatCompletionsPath = "chat/completions";

    /// <summary>Gets the default path, relative to <see cref="DefaultBaseAddress"/>, of the embeddings operation.</summary>
    public const string DefaultEmbeddingsPath = "embeddings";

    /// <summary>
    /// Gets the default capability set applied to a registered Mistral AI
    /// chat model unless the caller supplies its own.
    /// </summary>
    /// <remarks>
    /// This package's translator and parser do not yet round-trip Mistral's
    /// <c>ThinkChunk</c> reasoning content, whose exact incremental
    /// streaming shape (a content array that alternates with a plain
    /// string mid-stream) is not fully specified without live verification,
    /// so <c>SupportsReasoning</c> is <see langword="false"/> here. Image,
    /// document, and audio content parts, hosted hosted-tool declarations,
    /// and <c>response_format</c> structured output are not yet translated
    /// either, so vision and structured-output support remain
    /// <see langword="false"/>. A caller registering a model with
    /// materially different capabilities supplies its own
    /// <see cref="ModelCapabilities"/> rather than relying on this shared
    /// default.
    /// </remarks>
    public static ModelCapabilities DefaultCapabilities { get; } = new(
        supportsSystemInstructions: true,
        supportsStreaming: true,
        supportsToolCalls: true,
        supportsParallelToolCalls: true,
        supportsStructuredOutput: false,
        supportsReasoning: false,
        supportsVisionInput: false,
        ExtensionData.Empty);

    /// <summary>
    /// Gets the default, unbounded model limits applied to a registered
    /// Mistral AI chat model unless the caller supplies its own.
    /// </summary>
    public static ModelLimits DefaultLimits { get; } = new(maxContextTokens: null, maxOutputTokens: null);

    /// <summary>
    /// Gets the default capability set applied to a registered Mistral AI
    /// embedding model unless the caller supplies its own.
    /// </summary>
    /// <remarks>
    /// Mistral's embeddings API has no task-type/purpose parameter and no
    /// explicit truncation-policy control, but natively supports
    /// requesting a reduced output dimensionality and every quantized or
    /// packed-binary encoding this repository models. A caller registering
    /// a model with materially different capabilities supplies its own
    /// <see cref="EmbeddingCapabilities"/> rather than relying on this
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
    /// Mistral AI embedding model unless the caller supplies its own.
    /// </summary>
    public static EmbeddingLimits DefaultEmbeddingLimits { get; } =
        new(maxInputsPerRequest: null, maxInputTokensPerInput: null, defaultDimensions: null, maxDimensions: null);

    /// <summary>Builds the absolute chat completions operation URI for the given options.</summary>
    /// <param name="options">The validated Mistral AI provider options.</param>
    /// <returns>The absolute URI of the chat completions operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    public static Uri BuildChatCompletionsUri(MistralAIProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new Uri(options.BaseAddress, options.ChatCompletionsPath);
    }

    /// <summary>Builds the absolute embeddings operation URI for the given options.</summary>
    /// <param name="options">The validated Mistral AI provider options.</param>
    /// <returns>The absolute URI of the embeddings operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    public static Uri BuildEmbeddingsUri(MistralAIProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new Uri(options.BaseAddress, options.EmbeddingsPath);
    }
}
