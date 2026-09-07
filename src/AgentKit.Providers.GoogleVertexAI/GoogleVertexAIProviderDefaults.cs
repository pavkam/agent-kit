// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleVertexAI;

/// <summary>
/// The fixed identity and capability defaults for the Google Cloud Vertex
/// AI native <c>generateContent</c> integration.
/// </summary>
/// <remarks>
/// This package covers only Vertex's native
/// <c>publisherModel:generateContent</c>/<c>:streamGenerateContent</c>
/// operations for Gemini models, reusing
/// <see cref="IGoogleGeminiContentTranslator"/> and
/// <see cref="IGoogleGeminiResponseParser"/> from
/// <c>AgentKit.Providers.GoogleGemini</c> because Vertex's
/// <c>GenerateContentRequest</c>/<c>GenerateContentResponse</c> wire shape
/// closely follows the Gemini Developer API's native model; only resource
/// routing, authentication, and error status detail differ. Generic
/// Predict, the OpenAI-compatible endpoint, context caches, batch and
/// tuning jobs, the Discovery Engine Ranking API, and the bidirectional
/// Live protocol are separate contracts this package does not translate.
/// </remarks>
public static class GoogleVertexAIProviderDefaults
{
    /// <summary>Gets the stable <see cref="ProviderId"/> for Google Vertex AI.</summary>
    public static ProviderId ProviderId { get; } = new("google-vertex-ai");

    /// <summary>Gets the stable <see cref="ApiFamilyId"/> for Vertex's native generateContent wire format.</summary>
    public static ApiFamilyId ApiFamily { get; } = new("google-vertex-ai-generate-content");

    /// <summary>Gets the default REST API version path segment.</summary>
    public const string DefaultApiVersion = "v1";

    /// <summary>Gets the default publisher namespace for Google-published models such as Gemini.</summary>
    public const string DefaultPublisher = "google";

    /// <summary>
    /// Gets the default capability set applied to a registered Vertex AI
    /// chat model unless the caller supplies its own.
    /// </summary>
    /// <remarks>
    /// These defaults mirror <see cref="GoogleGeminiProviderDefaults.DefaultCapabilities"/>,
    /// since Vertex hosts the same Gemini models through the same
    /// GenerateContent content semantics reused by this package.
    /// </remarks>
    public static ModelCapabilities DefaultCapabilities { get; } = GoogleGeminiProviderDefaults.DefaultCapabilities;

    /// <summary>
    /// Gets the default, unbounded model limits applied to a registered
    /// Vertex AI chat model unless the caller supplies its own.
    /// </summary>
    public static ModelLimits DefaultLimits { get; } = new(maxContextTokens: null, maxOutputTokens: null);

    /// <summary>Gets the regional base address for the given location, such as <c>us-central1</c>.</summary>
    /// <param name="location">The Google Cloud region hosting the request.</param>
    /// <returns>The regional Vertex AI REST base address.</returns>
    /// <exception cref="ArgumentException"><paramref name="location"/> is null, empty, or consists only of whitespace.</exception>
    public static Uri BuildRegionalBaseAddress(string location)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(location);

        return new Uri($"https://{location}-aiplatform.googleapis.com/");
    }

    /// <summary>
    /// Builds the absolute <c>generateContent</c> or
    /// <c>streamGenerateContent</c> operation URI for the given options,
    /// model, optional deployed-endpoint override, and streaming
    /// preference.
    /// </summary>
    /// <param name="options">The validated Vertex AI provider options.</param>
    /// <param name="modelId">The publisher model identifier to embed in the operation path.</param>
    /// <param name="deploymentId">
    /// When set, the ID of a deployed/custom Vertex AI endpoint to call
    /// instead of the shared publisher-model resource.
    /// </param>
    /// <param name="useStreaming">Whether to build the streaming operation URI.</param>
    /// <returns>The absolute URI of the requested operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    public static Uri BuildGenerateContentUri(
        GoogleVertexAIProviderOptions options,
        ModelId modelId,
        DeploymentId? deploymentId,
        bool useStreaming)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ProjectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Location);

        var resource = deploymentId is { } deployment
            ? $"projects/{options.ProjectId}/locations/{options.Location}/endpoints/{deployment.Value}"
            : $"projects/{options.ProjectId}/locations/{options.Location}/publishers/{options.Publisher}/models/{modelId.Value}";

        var operation = useStreaming ? "streamGenerateContent" : "generateContent";
        var baseAddress = BuildRegionalBaseAddress(options.Location);
        var uri = new Uri(baseAddress, $"{options.ApiVersion}/{resource}:{operation}");

        return useStreaming ? new Uri($"{uri}?alt=sse") : uri;
    }
}
