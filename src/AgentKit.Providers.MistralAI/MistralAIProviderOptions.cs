// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.MistralAI;

/// <summary>
/// Configures the Mistral AI integration's endpoint and wire-behavior
/// defaults.
/// </summary>
/// <remarks>
/// This is a mutable options class following the standard
/// <c>Microsoft.Extensions.Options</c> pattern; it is bound and validated
/// once at composition time and consumed as an immutable
/// <see cref="Microsoft.Extensions.Options.IOptions{TOptions}"/> value
/// afterward. Authentication is configured separately, through
/// <c>AddMistralAIApiKeyCredential</c> or
/// <c>AddMistralAIOAuthCredential</c>, because credential material is never
/// appropriate to bind from ordinary configuration alongside endpoint
/// options.
/// </remarks>
public sealed class MistralAIProviderOptions
{
    /// <summary>Gets or sets the base address of the Mistral AI API.</summary>
    public Uri BaseAddress { get; set; } = MistralAIProviderDefaults.DefaultBaseAddress;

    /// <summary>
    /// Gets or sets the path, relative to <see cref="BaseAddress"/>, of the
    /// chat completions operation.
    /// </summary>
    public string ChatCompletionsPath { get; set; } = MistralAIProviderDefaults.DefaultChatCompletionsPath;

    /// <summary>
    /// Gets or sets the path, relative to <see cref="BaseAddress"/>, of the
    /// embeddings operation.
    /// </summary>
    public string EmbeddingsPath { get; set; } = MistralAIProviderDefaults.DefaultEmbeddingsPath;

    /// <summary>
    /// Gets or sets whether a request should prefer the streaming chat
    /// completions operation when the selected model supports streaming.
    /// </summary>
    public bool PreferStreaming { get; set; } = true;
}
