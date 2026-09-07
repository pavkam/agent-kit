// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible;

/// <summary>
/// The tested, explicit wire-behavior configuration one concrete
/// OpenAI-compatible provider package supplies to
/// <see cref="OpenAICompatibleChatModelBase"/>.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization.
/// </para>
/// <para>
/// A compatibility profile is evidence-backed configuration, never a
/// license to assume every OpenAI-shaped endpoint behaves identically. Each
/// concrete provider package (OpenAI itself, or a future compatible
/// integration) supplies its own tested profile instance rather than
/// reusing a shared default that might not reflect its actual verified
/// behavior.
/// </para>
/// </remarks>
public sealed record OpenAICompatibilityProfile
{
    /// <summary>Initializes a new instance of the <see cref="OpenAICompatibilityProfile"/> record.</summary>
    /// <param name="baseAddress">The absolute base address of the provider's OpenAI-compatible endpoint.</param>
    /// <param name="chatCompletionsPath">The path, relative to <paramref name="baseAddress"/>, of the chat completions operation.</param>
    /// <param name="sendDeveloperRoleAsSystem">
    /// Whether a <c>DeveloperMessage</c> should be translated using the
    /// legacy <c>"system"</c> role instead of the newer <c>"developer"</c>
    /// role.
    /// </param>
    /// <param name="preferStreaming">
    /// Whether the adapter should request the streaming (<c>stream: true</c>)
    /// operation by default rather than a single buffered response.
    /// </param>
    /// <param name="includeStreamUsage">
    /// Whether a streaming request should ask the provider to include a
    /// final usage-only chunk (<c>stream_options.include_usage</c>).
    /// </param>
    /// <param name="useMaxCompletionTokensField">
    /// Whether the output-token limit should be sent using the current
    /// <c>max_completion_tokens</c> field name instead of the legacy
    /// <c>max_tokens</c> field name.
    /// </param>
    /// <param name="defaultRequestHeaders">
    /// Additional headers to send with every request, beyond authentication
    /// headers.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="baseAddress"/>, <paramref name="chatCompletionsPath"/>,
    /// or <paramref name="defaultRequestHeaders"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="baseAddress"/> is not an absolute URI, or
    /// <paramref name="chatCompletionsPath"/> is empty, whitespace, rooted,
    /// authority-relative, or absolute.
    /// </exception>
    public OpenAICompatibilityProfile(
        Uri baseAddress,
        string chatCompletionsPath,
        bool sendDeveloperRoleAsSystem,
        bool preferStreaming,
        bool includeStreamUsage,
        bool useMaxCompletionTokensField,
        ImmutableDictionary<string, string> defaultRequestHeaders)
    {
        ArgumentException.ThrowIfNotAbsoluteUri(baseAddress);
        ArgumentException.ThrowIfNotRelativeUriPath(chatCompletionsPath);
        ArgumentNullException.ThrowIfNull(defaultRequestHeaders);

        BaseAddress = baseAddress;
        ChatCompletionsPath = chatCompletionsPath;
        SendDeveloperRoleAsSystem = sendDeveloperRoleAsSystem;
        PreferStreaming = preferStreaming;
        IncludeStreamUsage = includeStreamUsage;
        UseMaxCompletionTokensField = useMaxCompletionTokensField;
        DefaultRequestHeaders = defaultRequestHeaders;
    }

    /// <summary>Gets the absolute base address of the provider's OpenAI-compatible endpoint.</summary>
    public Uri BaseAddress { get; }

    /// <summary>Gets the path, relative to <see cref="BaseAddress"/>, of the chat completions operation.</summary>
    public string ChatCompletionsPath { get; }

    /// <summary>
    /// Gets whether a <c>DeveloperMessage</c> should be translated using
    /// the legacy <c>"system"</c> role instead of the newer
    /// <c>"developer"</c> role.
    /// </summary>
    public bool SendDeveloperRoleAsSystem { get; }

    /// <summary>
    /// Gets whether the adapter should request the streaming
    /// (<c>stream: true</c>) operation by default rather than a single
    /// buffered response.
    /// </summary>
    public bool PreferStreaming { get; }

    /// <summary>
    /// Gets whether a streaming request should ask the provider to include
    /// a final usage-only chunk (<c>stream_options.include_usage</c>).
    /// </summary>
    public bool IncludeStreamUsage { get; }

    /// <summary>
    /// Gets whether the output-token limit should be sent using the current
    /// <c>max_completion_tokens</c> field name instead of the legacy
    /// <c>max_tokens</c> field name.
    /// </summary>
    public bool UseMaxCompletionTokensField { get; }

    /// <summary>Gets additional headers to send with every request, beyond authentication headers.</summary>
    public ImmutableDictionary<string, string> DefaultRequestHeaders { get; }

    /// <summary>Gets the absolute URI of the chat completions operation.</summary>
    public Uri ChatCompletionsUri => new(BaseAddress, ChatCompletionsPath);
}
