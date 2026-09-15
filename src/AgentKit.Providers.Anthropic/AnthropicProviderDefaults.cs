// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Anthropic;

using AgentKit.Providers.Http;

/// <summary>
/// The fixed identity, endpoint, and capability defaults for the Anthropic
/// Claude integration.
/// </summary>
/// <remarks>
/// This package covers only the general-availability, stateless Messages
/// API (<c>POST /v1/messages</c>), including its streaming, tool-use, and
/// extended-thinking behavior. Anthropic's token-counting, Message
/// Batches, Files, Models, and Managed Agents endpoints are separate
/// contracts not covered by this package. Claude served through Amazon
/// Bedrock or Google Vertex AI uses those platforms' own authentication,
/// endpoints, and content envelopes and is not served by this package
/// either; per Anthropic's own guidance, a broker integration reuses the
/// canonical Claude message semantics rather than this direct HTTP
/// adapter.
/// </remarks>
public static class AnthropicProviderDefaults
{
    /// <summary>Gets the stable <see cref="ProviderId"/> for Anthropic.</summary>
    public static ProviderId ProviderId { get; } = new("anthropic");

    /// <summary>
    /// Gets the verified header authentication scheme for the Anthropic
    /// Messages API: an <see cref="ApiKeyProviderCredential"/> is sent as
    /// <c>x-api-key: &lt;key&gt;</c>, matching Anthropic's documented direct
    /// API-key authentication, while an <see cref="OAuthTokenProviderCredential"/>
    /// (for example, from Workload Identity Federation) is sent as
    /// <c>Authorization: Bearer &lt;token&gt;</c>.
    /// </summary>
    public static ProviderAuthorizationScheme AuthorizationScheme { get; } = ProviderAuthorizationScheme.ForApiKeyHeader("x-api-key");

    /// <summary>Gets the stable <see cref="ApiFamilyId"/> for Anthropic's Messages wire format.</summary>
    public static ApiFamilyId ApiFamily { get; } = new("anthropic-messages");

    /// <summary>Gets Anthropic's public REST API base address.</summary>
    public static Uri DefaultBaseAddress { get; } = new("https://api.anthropic.com/");

    /// <summary>Gets the default Messages operation path.</summary>
    public const string DefaultMessagesPath = "v1/messages";

    /// <summary>Gets the default <c>anthropic-version</c> header value selecting the stable wire contract.</summary>
    public const string DefaultAnthropicVersion = "2023-06-01";

    /// <summary>
    /// Gets the default output-token ceiling applied to a request that
    /// specifies neither <see cref="LlmRequestSettings.MaxOutputTokens"/>
    /// nor a configured <see cref="ModelLimits.MaxOutputTokens"/>, since
    /// Anthropic's <c>max_tokens</c> field is required and has no
    /// server-side default.
    /// </summary>
    public const long DefaultMaxOutputTokensFallback = 4096;

    /// <summary>
    /// Gets the default capability set applied to a registered Anthropic
    /// chat model unless the caller supplies its own.
    /// </summary>
    /// <remarks>
    /// This package's translator and parser round-trip Claude's
    /// <c>thinking</c> and <c>redacted_thinking</c> blocks, so
    /// <c>SupportsReasoning</c> is <see langword="true"/> unlike this
    /// repository's OpenAI-compatible provider defaults. Image and
    /// document content blocks, citations, and native structured-output
    /// schemas are not yet translated, so vision and structured-output
    /// support remain <see langword="false"/> here even though some Claude
    /// models support them natively. A caller registering a model with
    /// materially different capabilities supplies its own
    /// <see cref="ModelCapabilities"/> rather than relying on this shared
    /// default.
    /// </remarks>
    public static ModelCapabilities DefaultCapabilities { get; } = new(
        supportsSystemInstructions: true,
        supportsStreaming: true,
        supportsToolCalls: true,
        supportsParallelToolCalls: true,
        supportsStructuredOutput: false,
        supportsReasoning: true,
        supportsVisionInput: false,
        ExtensionData.Empty);

    /// <summary>
    /// Gets the default, unbounded model limits applied to a registered
    /// Anthropic chat model unless the caller supplies its own.
    /// </summary>
    public static ModelLimits DefaultLimits { get; } = new(maxContextTokens: null, maxOutputTokens: null);

    /// <summary>
    /// Builds the absolute Messages operation URI for the current
    /// <paramref name="options"/>.
    /// </summary>
    /// <param name="options">The validated Anthropic provider options.</param>
    /// <returns>The absolute URI of the Messages operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    public static Uri BuildMessagesUri(AnthropicProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new Uri(options.BaseAddress, options.MessagesPath);
    }
}
