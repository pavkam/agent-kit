// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.XAI;

using AgentKit.Providers.OpenAICompatible;

/// <summary>
/// The fixed identity, endpoint, and capability defaults for the xAI
/// integration.
/// </summary>
/// <remarks>
/// This package covers only xAI's OpenAI-shaped Chat Completions
/// endpoint (<c>POST /v1/chat/completions</c>). xAI's Responses API,
/// embeddings, files/collections search, image/video generation,
/// speech, realtime voice, batch, and first-party gRPC surface are
/// separate contracts not covered by this package.
/// </remarks>
public static class XAIProviderDefaults
{
    /// <summary>Gets the stable <see cref="ProviderId"/> for xAI.</summary>
    public static ProviderId ProviderId { get; } = new("xai");

    /// <summary>Gets the stable <see cref="ApiFamilyId"/> for xAI's Chat Completions wire format.</summary>
    public static ApiFamilyId ApiFamily { get; } = new("xai-chat-completions");

    /// <summary>Gets xAI's REST API base address.</summary>
    public static Uri DefaultBaseAddress { get; } = new("https://api.x.ai/v1/");

    /// <summary>Gets the default chat completions operation path.</summary>
    public const string DefaultChatCompletionsPath = "chat/completions";

    /// <summary>
    /// Gets the default capability set applied to a registered xAI
    /// chat model unless the caller supplies its own.
    /// </summary>
    /// <remarks>
    /// xAI's reference distinguishes supported, accepted-but-ignored,
    /// rejected, and unknown parameter support per model; a
    /// successfully sent request is not proof that every requested
    /// field took effect. A caller registering a model with materially
    /// different capabilities supplies its own
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
    /// xAI chat model unless the caller supplies its own.
    /// </summary>
    public static ModelLimits DefaultLimits { get; } = new(maxContextTokens: null, maxOutputTokens: null);

    /// <summary>
    /// Creates the <see cref="OpenAICompatibilityProfile"/> for the current
    /// <paramref name="options"/>.
    /// </summary>
    /// <param name="options">The validated xAI provider options.</param>
    /// <returns>A compatibility profile configured for xAI's Chat Completions endpoint.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    public static OpenAICompatibilityProfile CreateProfile(XAIProviderOptions options)
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
