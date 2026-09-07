// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Ollama;

using AgentKit.Providers.OpenAICompatible;

/// <summary>
/// The fixed identity, endpoint, and capability defaults for the Ollama
/// integration.
/// </summary>
/// <remarks>
/// This package covers only Ollama's OpenAI compatibility Chat
/// Completions endpoint (<c>POST /v1/chat/completions</c> on the local
/// or cloud OpenAI-compatible base). Ollama's native
/// <c>/api/chat</c>, <c>/api/generate</c>, and <c>/api/embed</c>
/// endpoints, its Anthropic Messages compatibility surface, and its
/// model-management endpoints (pull/push/create/copy/delete) are
/// separate contracts not covered by this package.
/// </remarks>
public static class OllamaProviderDefaults
{
    /// <summary>Gets the stable <see cref="ProviderId"/> for Ollama.</summary>
    public static ProviderId ProviderId { get; } = new("ollama");

    /// <summary>Gets the stable <see cref="ApiFamilyId"/> for Ollama's Chat Completions wire format.</summary>
    public static ApiFamilyId ApiFamily { get; } = new("ollama-openai-compatible");

    /// <summary>Gets Ollama's REST API base address.</summary>
    public static Uri DefaultBaseAddress { get; } = new("http://localhost:11434/v1/");

    /// <summary>Gets the default chat completions operation path.</summary>
    public const string DefaultChatCompletionsPath = "chat/completions";

    /// <summary>
    /// Gets the default capability set applied to a registered Ollama
    /// chat model unless the caller supplies its own.
    /// </summary>
    /// <remarks>
    /// Tool, vision, thinking, and structured-output support are
    /// determined by the locally installed or cloud-hosted model, not
    /// by the Ollama server itself; query <c>/api/show</c> for a
    /// specific model's capabilities rather than assuming this shared
    /// default applies universally. A caller registering a model with
    /// materially different capabilities supplies its own
    /// <see cref="ModelCapabilities"/> rather than relying on this
    /// shared default.
    /// </remarks>
    public static ModelCapabilities DefaultCapabilities { get; } = new(
        supportsSystemInstructions: true,
        supportsStreaming: true,
        supportsToolCalls: true,
        supportsParallelToolCalls: false,
        supportsStructuredOutput: true,
        supportsReasoning: false,
        supportsVisionInput: false,
        ExtensionData.Empty);

    /// <summary>
    /// Gets the default, unbounded model limits applied to a registered
    /// Ollama chat model unless the caller supplies its own.
    /// </summary>
    public static ModelLimits DefaultLimits { get; } = new(maxContextTokens: null, maxOutputTokens: null);

    /// <summary>
    /// Creates the <see cref="OpenAICompatibilityProfile"/> for the current
    /// <paramref name="options"/>.
    /// </summary>
    /// <param name="options">The validated Ollama provider options.</param>
    /// <returns>A compatibility profile configured for Ollama's Chat Completions endpoint.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    public static OpenAICompatibilityProfile CreateProfile(OllamaProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new OpenAICompatibilityProfile(
            options.BaseAddress,
            options.ChatCompletionsPath,
            sendDeveloperRoleAsSystem: true,
            preferStreaming: options.PreferStreaming,
            includeStreamUsage: options.IncludeStreamUsage,
            useMaxCompletionTokensField: false,
            []);
    }
}
