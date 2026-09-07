// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleGemini;

/// <summary>
/// The fixed identity, endpoint, and capability defaults for the Google
/// Gemini Developer API integration.
/// </summary>
/// <remarks>
/// This package covers only the API-key based Gemini Developer API's
/// <c>GenerateContent</c>/<c>StreamGenerateContent</c> operations
/// (<c>POST /v1beta/models/{model}:generateContent</c> and
/// <c>:streamGenerateContent?alt=sse</c>). The newer, server-stateful
/// Interactions API, embeddings, Live WebSocket, Files, cached content, and
/// batch generation are separate contracts not covered by this package.
/// Google Vertex AI hosts many of the same models under Google Cloud IAM
/// with different resource names and is a separate provider integration,
/// not this package.
/// </remarks>
public static class GoogleGeminiProviderDefaults
{
    /// <summary>Gets the stable <see cref="ProviderId"/> for Google Gemini.</summary>
    public static ProviderId ProviderId { get; } = new("google-gemini");

    /// <summary>Gets the stable <see cref="ApiFamilyId"/> for Gemini's GenerateContent wire format.</summary>
    public static ApiFamilyId ApiFamily { get; } = new("google-gemini-generate-content");

    /// <summary>Gets the Gemini Developer API's base address.</summary>
    public static Uri DefaultBaseAddress { get; } = new("https://generativelanguage.googleapis.com/");

    /// <summary>Gets the default API version path segment.</summary>
    public const string DefaultApiVersion = "v1beta";

    /// <summary>
    /// Gets the default capability set applied to a registered Gemini chat
    /// model unless the caller supplies its own.
    /// </summary>
    /// <remarks>
    /// This package's translator and parser round-trip Gemini's
    /// <c>thought</c>/<c>thoughtSignature</c> reasoning parts, so
    /// <c>SupportsReasoning</c> is <see langword="true"/>. Image, file, and
    /// executable-code content parts, citations, and native structured
    /// output are not yet translated, so vision and structured-output
    /// support remain <see langword="false"/> here even though some Gemini
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
    /// Gemini chat model unless the caller supplies its own.
    /// </summary>
    public static ModelLimits DefaultLimits { get; } = new(maxContextTokens: null, maxOutputTokens: null);

    /// <summary>
    /// Builds the absolute <c>generateContent</c> or
    /// <c>streamGenerateContent</c> operation URI for the given model and
    /// streaming preference.
    /// </summary>
    /// <param name="options">The validated Gemini provider options.</param>
    /// <param name="modelId">The model identifier to embed in the operation path.</param>
    /// <param name="useStreaming">Whether to build the streaming operation URI.</param>
    /// <returns>The absolute URI of the requested operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    public static Uri BuildGenerateContentUri(GoogleGeminiProviderOptions options, ModelId modelId, bool useStreaming)
    {
        ArgumentNullException.ThrowIfNull(options);

        var operation = useStreaming ? "streamGenerateContent" : "generateContent";
        var path = $"{options.ApiVersion}/models/{modelId.Value}:{operation}";
        var uri = new Uri(options.BaseAddress, path);

        return useStreaming ? new Uri($"{uri}?alt=sse") : uri;
    }
}
