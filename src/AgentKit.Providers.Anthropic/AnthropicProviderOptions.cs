// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Anthropic;

/// <summary>
/// Configures the Anthropic integration's endpoint and wire-behavior
/// defaults.
/// </summary>
/// <remarks>
/// This is a mutable options class following the standard
/// <c>Microsoft.Extensions.Options</c> pattern; it is bound and validated
/// once at composition time and consumed as an immutable
/// <see cref="Microsoft.Extensions.Options.IOptions{TOptions}"/> value
/// afterward. Authentication is configured separately, through
/// <c>AddAnthropicApiKeyCredential</c> or
/// <c>AddAnthropicOAuthCredential</c>, because credential material is
/// never appropriate to bind from ordinary configuration alongside
/// endpoint options.
/// </remarks>
public sealed class AnthropicProviderOptions
{
    /// <summary>Gets or sets the base address of the Anthropic REST API.</summary>
    public Uri BaseAddress { get; set; } = AnthropicProviderDefaults.DefaultBaseAddress;

    /// <summary>Gets or sets the Messages operation path, relative to <see cref="BaseAddress"/>.</summary>
    public string MessagesPath { get; set; } = AnthropicProviderDefaults.DefaultMessagesPath;

    /// <summary>
    /// Gets or sets the <c>anthropic-version</c> header value sent with
    /// every request, selecting the stable wire contract.
    /// </summary>
    public string AnthropicVersion { get; set; } = AnthropicProviderDefaults.DefaultAnthropicVersion;

    /// <summary>
    /// Gets or sets whether a request should prefer the streaming
    /// (<c>stream: true</c>) operation when the selected model supports
    /// streaming.
    /// </summary>
    public bool PreferStreaming { get; set; } = true;

    /// <summary>
    /// Gets or sets the output-token ceiling sent as <c>max_tokens</c>
    /// when a request specifies neither
    /// <see cref="LlmRequestSettings.MaxOutputTokens"/> nor a configured
    /// <see cref="ModelLimits.MaxOutputTokens"/>.
    /// </summary>
    public long DefaultMaxOutputTokens { get; set; } = AnthropicProviderDefaults.DefaultMaxOutputTokensFallback;
}
