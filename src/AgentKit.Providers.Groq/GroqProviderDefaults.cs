// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Groq;

using AgentKit.Providers.OpenAICompatible;

/// <summary>
/// The fixed identity, endpoint, and capability defaults for the Groq
/// integration.
/// </summary>
/// <remarks>
/// This package covers only Groq's low-latency OpenAI-shaped Chat
/// Completions endpoint (<c>POST /openai/v1/chat/completions</c>).
/// Groq's beta Responses endpoint, audio (transcription, translation,
/// speech), batch, and closed-beta fine-tuning surfaces are separate
/// contracts not covered by this package.
/// </remarks>
public static class GroqProviderDefaults
{
    /// <summary>Gets the stable <see cref="ProviderId"/> for Groq.</summary>
    public static ProviderId ProviderId { get; } = new("groq");

    /// <summary>Gets the stable <see cref="ApiFamilyId"/> for Groq's Chat Completions wire format.</summary>
    public static ApiFamilyId ApiFamily { get; } = new("groq-chat-completions");

    /// <summary>Gets Groq's REST API base address.</summary>
    public static Uri DefaultBaseAddress { get; } = new("https://api.groq.com/openai/v1/");

    /// <summary>Gets the default chat completions operation path.</summary>
    public const string DefaultChatCompletionsPath = "chat/completions";

    /// <summary>
    /// Gets the default capability set applied to a registered Groq
    /// chat model unless the caller supplies its own.
    /// </summary>
    /// <remarks>
    /// Groq's Compound models add server-side tool orchestration through
    /// fields outside OpenAI's contract, and some models constrain or
    /// reject standard OpenAI parameters (for example, requiring
    /// <c>n</c> to be exactly one). A caller registering a model with
    /// materially different capabilities supplies its own
    /// <see cref="ModelCapabilities"/> rather than relying on this
    /// shared default.
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
    /// Groq chat model unless the caller supplies its own.
    /// </summary>
    public static ModelLimits DefaultLimits { get; } = new(maxContextTokens: null, maxOutputTokens: null);

    /// <summary>
    /// Creates the <see cref="OpenAICompatibilityProfile"/> for the current
    /// <paramref name="options"/>.
    /// </summary>
    /// <param name="options">The validated Groq provider options.</param>
    /// <returns>A compatibility profile configured for Groq's Chat Completions endpoint.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    public static OpenAICompatibilityProfile CreateProfile(GroqProviderOptions options)
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
