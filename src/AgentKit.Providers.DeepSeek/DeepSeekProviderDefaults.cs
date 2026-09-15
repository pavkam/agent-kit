// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.DeepSeek;

using AgentKit.Providers.OpenAICompatible;

/// <summary>
/// The fixed identity, endpoint, and capability defaults for the DeepSeek
/// integration.
/// </summary>
/// <remarks>
/// This package covers only DeepSeek's OpenAI-shaped Chat Completions
/// dialect (<c>POST /chat/completions</c> on the standard base). DeepSeek's
/// Responses dialect, its Anthropic Messages-compatible dialect
/// (<c>/anthropic/v1/messages</c>), its fill-in-the-middle beta completion
/// endpoint, and its Files API are separate wire contracts not covered by
/// this package.
/// </remarks>
public static class DeepSeekProviderDefaults
{
    /// <summary>Gets the stable <see cref="ProviderId"/> for DeepSeek.</summary>
    public static ProviderId ProviderId { get; } = new("deepseek");

    /// <summary>Gets the stable <see cref="ApiFamilyId"/> for DeepSeek's Chat Completions wire format.</summary>
    public static ApiFamilyId ApiFamily { get; } = new("deepseek-chat-completions");

    /// <summary>Gets DeepSeek's OpenAI-style REST API base address.</summary>
    public static Uri DefaultBaseAddress { get; } = new("https://api.deepseek.com/");

    /// <summary>Gets the default chat completions operation path.</summary>
    public const string DefaultChatCompletionsPath = "chat/completions";

    /// <summary>
    /// Gets the default capability set applied to a registered DeepSeek
    /// chat model unless the caller supplies its own.
    /// </summary>
    /// <remarks>
    /// DeepSeek's compatibility policy silently ignores some unsupported
    /// fields rather than rejecting them. Thinking-mode reasoning is returned
    /// on a separate <c>reasoning_content</c> field; the shared parser records
    /// it as a <see cref="ReasoningPart"/> and <see cref="CreateProfile"/>
    /// replays it on later assistant messages, as DeepSeek requires for
    /// requests that carry <c>tools</c>. This default still does not claim
    /// <see cref="ModelCapabilities.SupportsReasoning"/> because that flag
    /// describes the registered model, not the wire mechanics. A caller
    /// registering a model with materially different capabilities supplies
    /// its own <see cref="ModelCapabilities"/> rather than relying on this
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
    /// DeepSeek chat model unless the caller supplies its own.
    /// </summary>
    public static ModelLimits DefaultLimits { get; } = new(maxContextTokens: null, maxOutputTokens: null);

    /// <summary>
    /// Creates the <see cref="OpenAICompatibilityProfile"/> for the current
    /// <paramref name="options"/>.
    /// </summary>
    /// <param name="options">The validated DeepSeek provider options.</param>
    /// <returns>
    /// A compatibility profile configured for DeepSeek's Chat Completions endpoint. It selects
    /// <see cref="OpenAIAssistantReasoningReplay.ReasoningContentField"/> because DeepSeek's thinking-mode
    /// guide states that requests carrying <c>tools</c> must pass back every prior turn's
    /// <c>reasoning_content</c> or receive a 400, while requests without <c>tools</c> simply ignore it.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    public static OpenAICompatibilityProfile CreateProfile(DeepSeekProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new OpenAICompatibilityProfile(
            options.BaseAddress,
            options.ChatCompletionsPath,
            sendDeveloperRoleAsSystem: true,
            preferStreaming: options.PreferStreaming,
            includeStreamUsage: options.IncludeStreamUsage,
            useMaxCompletionTokensField: false,
            [])
        {
            AssistantReasoningReplay = OpenAIAssistantReasoningReplay.ReasoningContentField,
        };
    }
}
