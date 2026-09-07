// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenRouter;

/// <summary>
/// Configures the OpenRouter integration's endpoint, attribution, and
/// wire-behavior defaults.
/// </summary>
/// <remarks>
/// This is a mutable options class following the standard
/// <c>Microsoft.Extensions.Options</c> pattern; it is bound and validated
/// once at composition time and consumed as an immutable
/// <see cref="Microsoft.Extensions.Options.IOptions{TOptions}"/> value
/// afterward. Authentication is configured separately, through
/// <c>AddOpenRouterApiKeyCredential</c> or
/// <c>AddOpenRouterOAuthCredential</c>, because credential material is
/// never appropriate to bind from ordinary configuration alongside
/// endpoint options.
/// </remarks>
public sealed class OpenRouterProviderOptions
{
    /// <summary>Gets or sets the base address of the OpenRouter REST API.</summary>
    public Uri BaseAddress { get; set; } = OpenRouterProviderDefaults.DefaultBaseAddress;

    /// <summary>Gets or sets the chat completions operation path, relative to <see cref="BaseAddress"/>.</summary>
    public string ChatCompletionsPath { get; set; } = OpenRouterProviderDefaults.DefaultChatCompletionsPath;

    /// <summary>
    /// Gets or sets whether a request should prefer the streaming
    /// (<c>stream: true</c>) operation when the selected model supports
    /// streaming.
    /// </summary>
    public bool PreferStreaming { get; set; } = true;

    /// <summary>
    /// Gets or sets whether a streaming request should ask OpenRouter to
    /// include a final usage-only chunk.
    /// </summary>
    public bool IncludeStreamUsage { get; set; } = true;

    /// <summary>
    /// Gets or sets the application's canonical URL, sent as the optional
    /// <c>HTTP-Referer</c> attribution header, when set.
    /// </summary>
    public string? HttpReferer { get; set; }

    /// <summary>
    /// Gets or sets the application's display name, sent as the optional
    /// <c>X-OpenRouter-Title</c> attribution header, when set.
    /// </summary>
    public string? ApplicationTitle { get; set; }

    /// <summary>
    /// Gets or sets whether successful responses should opt into
    /// OpenRouter's <c>openrouter_metadata</c> routing-decision object via
    /// the <c>X-OpenRouter-Metadata: enabled</c> header.
    /// </summary>
    public bool IncludeRoutingMetadata { get; set; }
}
