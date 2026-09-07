// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Groq;

/// <summary>
/// Configures the Groq integration's endpoint and wire-behavior
/// defaults.
/// </summary>
/// <remarks>
/// This is a mutable options class following the standard
/// <c>Microsoft.Extensions.Options</c> pattern; it is bound and validated
/// once at composition time and consumed as an immutable
/// <see cref="Microsoft.Extensions.Options.IOptions{TOptions}"/> value
/// afterward. Authentication is configured separately, through
/// <c>AddGroqApiKeyCredential</c> or <c>AddGroqOAuthCredential</c>, because
/// credential material is never appropriate to bind from ordinary
/// configuration alongside endpoint options.
/// </remarks>
public sealed class GroqProviderOptions
{
    /// <summary>Gets or sets the base address of the Groq REST API.</summary>
    public Uri BaseAddress { get; set; } = GroqProviderDefaults.DefaultBaseAddress;

    /// <summary>Gets or sets the chat completions operation path, relative to <see cref="BaseAddress"/>.</summary>
    public string ChatCompletionsPath { get; set; } = GroqProviderDefaults.DefaultChatCompletionsPath;

    /// <summary>
    /// Gets or sets whether a request should prefer the streaming
    /// (<c>stream: true</c>) operation when the selected model supports
    /// streaming.
    /// </summary>
    public bool PreferStreaming { get; set; } = true;

    /// <summary>
    /// Gets or sets whether a streaming request should ask Groq to
    /// include a final usage-only chunk.
    /// </summary>
    public bool IncludeStreamUsage { get; set; } = true;
}
