// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AzureOpenAI;

/// <summary>
/// Configures the Azure OpenAI integration's endpoint and wire-behavior
/// defaults.
/// </summary>
/// <remarks>
/// This is a mutable options class following the standard
/// <c>Microsoft.Extensions.Options</c> pattern; it is bound and validated
/// once at composition time and consumed as an immutable
/// <see cref="Microsoft.Extensions.Options.IOptions{TOptions}"/> value
/// afterward. Authentication is configured separately, through
/// <c>AddAzureOpenAIApiKeyCredential</c> or
/// <c>AddAzureOpenAIOAuthCredential</c>, because credential material is
/// never appropriate to bind from ordinary configuration alongside
/// endpoint options. Unlike the public OpenAI API, an Azure OpenAI
/// resource endpoint is account-specific, so <see cref="ResourceEndpoint"/>
/// has no fabricated default; it must be configured explicitly.
/// </remarks>
public sealed class AzureOpenAIProviderOptions
{
    /// <summary>
    /// Gets or sets the caller's Azure OpenAI resource endpoint, such as
    /// <c>https://my-resource.openai.azure.com/</c>, or
    /// <see langword="null"/> if not yet configured.
    /// </summary>
    public Uri? ResourceEndpoint { get; set; }

    /// <summary>
    /// Gets or sets the GA v1 chat completions operation path, relative to
    /// <see cref="ResourceEndpoint"/>.
    /// </summary>
    public string ChatCompletionsPath { get; set; } = AzureOpenAIProviderDefaults.DefaultChatCompletionsPath;

    /// <summary>
    /// Gets or sets the GA v1 embeddings operation path, relative to
    /// <see cref="ResourceEndpoint"/>.
    /// </summary>
    public string EmbeddingsPath { get; set; } = AzureOpenAIProviderDefaults.DefaultEmbeddingsPath;

    /// <summary>
    /// Gets or sets whether a request should prefer the streaming
    /// (<c>stream: true</c>) operation when the selected model supports
    /// streaming.
    /// </summary>
    public bool PreferStreaming { get; set; } = true;

    /// <summary>
    /// Gets or sets whether a streaming request should ask Azure OpenAI to
    /// include a final usage-only chunk.
    /// </summary>
    public bool IncludeStreamUsage { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the output-token limit should be sent using the
    /// current <c>max_completion_tokens</c> field name instead of the
    /// legacy <c>max_tokens</c> field name.
    /// </summary>
    public bool UseMaxCompletionTokensField { get; set; } = true;
}
