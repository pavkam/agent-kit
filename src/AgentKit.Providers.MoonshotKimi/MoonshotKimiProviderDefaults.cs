// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.MoonshotKimi;

using AgentKit.Providers.OpenAICompatible;

/// <summary>
/// The fixed identity, endpoint, and capability defaults for the Moonshot Kimi
/// integration.
/// </summary>
/// <remarks>
/// This package covers only Kimi's OpenAI-shaped Chat Completions
/// dialect (<c>POST /v1/chat/completions</c>). Kimi's Responses
/// dialect, its Anthropic Messages-compatible dialect
/// (<c>/anthropic/v1/messages</c>), its request-proof signature
/// verification, and its Files/Batch APIs are separate contracts not
/// covered by this package.
/// </remarks>
public static class MoonshotKimiProviderDefaults
{
    /// <summary>Gets the stable <see cref="ProviderId"/> for Moonshot Kimi.</summary>
    public static ProviderId ProviderId { get; } = new("moonshot-kimi");

    /// <summary>Gets the stable <see cref="ApiFamilyId"/> for Moonshot Kimi's Chat Completions wire format.</summary>
    public static ApiFamilyId ApiFamily { get; } = new("moonshot-chat-completions");

    /// <summary>Gets Moonshot Kimi's REST API base address.</summary>
    public static Uri DefaultBaseAddress { get; } = new("https://api.moonshot.ai/v1/");

    /// <summary>Gets the default chat completions operation path.</summary>
    public const string DefaultChatCompletionsPath = "chat/completions";

    /// <summary>
    /// Gets the default capability set applied to a registered Moonshot Kimi
    /// chat model unless the caller supplies its own.
    /// </summary>
    /// <remarks>
    /// Kimi is stateless: history is always resent in full. Thinking
    /// models return reasoning on a separate <c>reasoning_content</c>
    /// field; the shared parser records it as a <see cref="ReasoningPart"/>
    /// and <see cref="CreateProfile"/> replays it on later assistant
    /// messages so multi-step tool-call loops keep their chain of thought,
    /// as Moonshot requires. This default still does not claim
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
    /// Moonshot Kimi chat model unless the caller supplies its own.
    /// </summary>
    public static ModelLimits DefaultLimits { get; } = new(maxContextTokens: null, maxOutputTokens: null);

    /// <summary>
    /// Creates the <see cref="OpenAICompatibilityProfile"/> for the current
    /// <paramref name="options"/>.
    /// </summary>
    /// <param name="options">The validated Moonshot Kimi provider options.</param>
    /// <returns>
    /// A compatibility profile configured for Moonshot Kimi's Chat Completions endpoint. It selects
    /// <see cref="OpenAIAssistantReasoningReplay.ReasoningContentField"/> because Moonshot's thinking-model
    /// guide requires the complete assistant message, including <c>reasoning_content</c>, to be passed back
    /// within a tool-call loop and across turns for models with Preserved Thinking; models that do not keep
    /// historical reasoning ignore the field rather than rejecting it.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    public static OpenAICompatibilityProfile CreateProfile(MoonshotKimiProviderOptions options)
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
