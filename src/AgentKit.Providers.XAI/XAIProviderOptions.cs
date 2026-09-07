// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.XAI;

/// <summary>
/// Configures the xAI integration's endpoint and wire-behavior
/// defaults.
/// </summary>
/// <remarks>
/// This is a mutable options class following the standard
/// <c>Microsoft.Extensions.Options</c> pattern; it is bound and validated
/// once at composition time and consumed as an immutable
/// <see cref="Microsoft.Extensions.Options.IOptions{TOptions}"/> value
/// afterward. Authentication is configured separately, through
/// <c>AddXAIApiKeyCredential</c> or <c>AddXAIOAuthCredential</c>, because
/// credential material is never appropriate to bind from ordinary
/// configuration alongside endpoint options.
/// </remarks>
public sealed class XAIProviderOptions
{
    /// <summary>Gets or sets the base address of the xAI REST API.</summary>
    public Uri BaseAddress { get; set; } = XAIProviderDefaults.DefaultBaseAddress;

    /// <summary>Gets or sets the chat completions operation path, relative to <see cref="BaseAddress"/>.</summary>
    public string ChatCompletionsPath { get; set; } = XAIProviderDefaults.DefaultChatCompletionsPath;

    /// <summary>
    /// Gets or sets whether a request should prefer the streaming
    /// (<c>stream: true</c>) operation when the selected model supports
    /// streaming.
    /// </summary>
    public bool PreferStreaming { get; set; } = true;

    /// <summary>
    /// Gets or sets whether a streaming request should ask xAI to
    /// include a final usage-only chunk.
    /// </summary>
    public bool IncludeStreamUsage { get; set; } = true;
}
