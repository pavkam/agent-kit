// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenRouter;

using AgentKit.Providers.OpenAICompatible;

/// <summary>
/// The fixed identity, endpoint, and capability defaults for the OpenRouter
/// integration.
/// </summary>
/// <remarks>
/// This package covers only OpenRouter's Chat Completions dialect (the
/// "extended OpenAI Chat Completions" skin documented for
/// <c>POST /chat/completions</c>). OpenRouter's Responses and Anthropic
/// Messages skins, embeddings, and reranking are separate wire contracts
/// and separate <see cref="ILlmModel"/>/semantic-operation
/// implementations not covered by this package.
/// </remarks>
public static class OpenRouterProviderDefaults
{
    /// <summary>Gets the stable <see cref="ProviderId"/> for OpenRouter.</summary>
    public static ProviderId ProviderId { get; } = new("openrouter");

    /// <summary>Gets the stable <see cref="ApiFamilyId"/> for OpenRouter's Chat Completions wire format.</summary>
    public static ApiFamilyId ApiFamily { get; } = new("openrouter-chat-completions");

    /// <summary>Gets OpenRouter's public REST API base address.</summary>
    public static Uri DefaultBaseAddress { get; } = new("https://openrouter.ai/api/v1/");

    /// <summary>Gets the default chat completions operation path.</summary>
    public const string DefaultChatCompletionsPath = "chat/completions";

    /// <summary>
    /// Gets the default capability set applied to a registered OpenRouter
    /// chat model unless the caller supplies its own.
    /// </summary>
    /// <remarks>
    /// OpenRouter routes a request to many different upstream models and
    /// providers, so actual capability support varies by the selected
    /// model. A caller registering a model whose upstream provider has
    /// materially different capabilities supplies its own
    /// <see cref="ModelCapabilities"/> rather than relying on this shared
    /// default.
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
    /// OpenRouter chat model unless the caller supplies its own.
    /// </summary>
    public static ModelLimits DefaultLimits { get; } = new(maxContextTokens: null, maxOutputTokens: null);

    /// <summary>
    /// Creates the <see cref="OpenAICompatibilityProfile"/> for the current
    /// <paramref name="options"/>, including OpenRouter's optional
    /// attribution and routing-metadata headers.
    /// </summary>
    /// <param name="options">The validated OpenRouter provider options.</param>
    /// <returns>A compatibility profile configured for OpenRouter's public REST API.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    public static OpenAICompatibilityProfile CreateProfile(OpenRouterProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var headers = ImmutableDictionary<string, string>.Empty;

        if (options.HttpReferer is { Length: > 0 } referer)
        {
            headers = headers.Add("HTTP-Referer", referer);
        }

        if (options.ApplicationTitle is { Length: > 0 } title)
        {
            headers = headers.Add("X-OpenRouter-Title", title);
        }

        if (options.IncludeRoutingMetadata)
        {
            headers = headers.Add("X-OpenRouter-Metadata", "enabled");
        }

        return new OpenAICompatibilityProfile(
            options.BaseAddress,
            options.ChatCompletionsPath,
            sendDeveloperRoleAsSystem: false,
            preferStreaming: options.PreferStreaming,
            includeStreamUsage: options.IncludeStreamUsage,
            useMaxCompletionTokensField: false,
            headers);
    }
}
