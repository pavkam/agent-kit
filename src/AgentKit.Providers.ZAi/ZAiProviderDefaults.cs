// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.ZAi;

using AgentKit.Providers.OpenAICompatible;

/// <summary>
/// The fixed identity, endpoint, and capability defaults for the Z.ai
/// integration.
/// </summary>
/// <remarks>
/// This package covers only Z.ai's Chat Completions operation
/// (<c>POST /chat/completions</c>). Z.ai's image, video, audio, document,
/// search, tokenizer, and agent endpoints are separate, non-conversational
/// contracts not covered by this package; in particular, Z.ai's public
/// contract exposes no embeddings endpoint, so no embedding operation is
/// registered here.
/// </remarks>
public static class ZAiProviderDefaults
{
    /// <summary>Gets the stable <see cref="ProviderId"/> for Z.ai.</summary>
    public static ProviderId ProviderId { get; } = new("z-ai");

    /// <summary>Gets the stable <see cref="ApiFamilyId"/> for Z.ai's Chat Completions wire format.</summary>
    public static ApiFamilyId ApiFamily { get; } = new("z-ai-chat-completions");

    /// <summary>Gets Z.ai's general-purpose PaaS REST API base address.</summary>
    public static Uri DefaultBaseAddress { get; } = new("https://api.z.ai/api/paas/v4/");

    /// <summary>Gets Z.ai's coding-plan REST API base address, a separate routing/billing surface.</summary>
    public static Uri CodingPlanBaseAddress { get; } = new("https://api.z.ai/api/coding/paas/v4/");

    /// <summary>Gets the default chat completions operation path.</summary>
    public const string DefaultChatCompletionsPath = "chat/completions";

    /// <summary>
    /// Gets the default capability set applied to a registered Z.ai chat
    /// model unless the caller supplies its own.
    /// </summary>
    /// <remarks>
    /// Sampling and token fields are not uniform across GLM model
    /// generations, and tool-choice support for a forced named function is
    /// not documented as universally available. A caller registering a
    /// model with materially different capabilities supplies its own
    /// <see cref="ModelCapabilities"/> rather than relying on this shared
    /// default.
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
    /// Z.ai chat model unless the caller supplies its own.
    /// </summary>
    public static ModelLimits DefaultLimits { get; } = new(maxContextTokens: null, maxOutputTokens: null);

    /// <summary>
    /// Creates the <see cref="OpenAICompatibilityProfile"/> for the current
    /// <paramref name="options"/>.
    /// </summary>
    /// <param name="options">The validated Z.ai provider options.</param>
    /// <returns>A compatibility profile configured for Z.ai's Chat Completions endpoint.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    public static OpenAICompatibilityProfile CreateProfile(ZAiProviderOptions options)
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
