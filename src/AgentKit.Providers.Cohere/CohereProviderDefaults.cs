// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Cohere;

/// <summary>
/// The fixed identity, endpoint, and capability defaults for the Cohere v2
/// Chat integration.
/// </summary>
/// <remarks>
/// This package covers only <c>POST /v2/chat</c> (buffered and streaming)
/// for text, reasoning, and client-executed function tools. Embed, Rerank,
/// Classify, the legacy v1 dialect, and asynchronous embed jobs are
/// separate contracts not covered by this package. Grounding documents,
/// citations, <c>response_format</c> structured output, <c>safety_mode</c>,
/// and hosted connectors are not yet translated.
/// </remarks>
public static class CohereProviderDefaults
{
    /// <summary>Gets the stable <see cref="ProviderId"/> for Cohere.</summary>
    public static ProviderId ProviderId { get; } = new("cohere");

    /// <summary>Gets the stable <see cref="ApiFamilyId"/> for Cohere's v2 Chat wire format.</summary>
    public static ApiFamilyId ApiFamily { get; } = new("cohere-chat-v2");

    /// <summary>Gets the Cohere API's base address.</summary>
    public static Uri DefaultBaseAddress { get; } = new("https://api.cohere.com/");

    /// <summary>Gets the default path, relative to <see cref="DefaultBaseAddress"/>, of the chat operation.</summary>
    public const string DefaultChatPath = "v2/chat";

    /// <summary>
    /// Gets the default capability set applied to a registered Cohere chat
    /// model unless the caller supplies its own.
    /// </summary>
    /// <remarks>
    /// This package's translator and parser round-trip Cohere's
    /// <c>thinking</c> content blocks through the same event-driven
    /// content-start/content-delta/content-end grammar used for visible
    /// text, so <c>SupportsReasoning</c> is <see langword="true"/>. Image
    /// and document content parts, grounding documents, citations, hosted
    /// connectors, and native structured output are not yet translated, so
    /// vision and structured-output support remain <see langword="false"/>
    /// here even though some Cohere models support them natively. A caller
    /// registering a model with materially different capabilities supplies
    /// its own <see cref="ModelCapabilities"/> rather than relying on this
    /// shared default.
    /// </remarks>
    public static ModelCapabilities DefaultCapabilities { get; } = new(
        supportsSystemInstructions: true,
        supportsStreaming: true,
        supportsToolCalls: true,
        supportsParallelToolCalls: true,
        supportsStructuredOutput: false,
        supportsReasoning: true,
        supportsVisionInput: false,
        ExtensionData.Empty);

    /// <summary>
    /// Gets the default, unbounded model limits applied to a registered
    /// Cohere chat model unless the caller supplies its own.
    /// </summary>
    public static ModelLimits DefaultLimits { get; } = new(maxContextTokens: null, maxOutputTokens: null);

    /// <summary>Builds the absolute chat operation URI for the given options.</summary>
    /// <param name="options">The validated Cohere provider options.</param>
    /// <returns>The absolute URI of the chat operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    public static Uri BuildChatUri(CohereProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new Uri(options.BaseAddress, options.ChatPath);
    }
}
