// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.ZAi;

/// <summary>
/// Configures the Z.ai integration's endpoint and wire-behavior defaults.
/// </summary>
/// <remarks>
/// This is a mutable options class following the standard
/// <c>Microsoft.Extensions.Options</c> pattern; it is bound and validated
/// once at composition time and consumed as an immutable
/// <see cref="Microsoft.Extensions.Options.IOptions{TOptions}"/> value
/// afterward. Authentication is configured separately, through
/// <c>AddZAiApiKeyCredential</c> or <c>AddZAiOAuthCredential</c>, because
/// credential material is never appropriate to bind from ordinary
/// configuration alongside endpoint options.
/// </remarks>
public sealed class ZAiProviderOptions
{
    /// <summary>
    /// Gets or sets the base address of the Z.ai REST API. Defaults to the
    /// general-purpose base; set this to
    /// <see cref="ZAiProviderDefaults.CodingPlanBaseAddress"/> to use the
    /// separate coding-plan routing and billing surface instead.
    /// </summary>
    public Uri BaseAddress { get; set; } = ZAiProviderDefaults.DefaultBaseAddress;

    /// <summary>Gets or sets the chat completions operation path, relative to <see cref="BaseAddress"/>.</summary>
    public string ChatCompletionsPath { get; set; } = ZAiProviderDefaults.DefaultChatCompletionsPath;

    /// <summary>
    /// Gets or sets whether a request should prefer the streaming
    /// (<c>stream: true</c>) operation when the selected model supports
    /// streaming.
    /// </summary>
    public bool PreferStreaming { get; set; } = true;

    /// <summary>
    /// Gets or sets whether a streaming request should ask Z.ai to include
    /// a final usage-only chunk.
    /// </summary>
    public bool IncludeStreamUsage { get; set; } = true;
}
