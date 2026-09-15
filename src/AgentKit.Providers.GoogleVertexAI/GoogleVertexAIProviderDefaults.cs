// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleVertexAI;

using AgentKit.Providers.Http;

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

    /// <summary>
    /// Gets the verified header authentication scheme for the Vertex AI REST
    /// API. Vertex documents exactly one mechanism,
    /// <c>Authorization: Bearer &lt;token&gt;</c> from Google OAuth 2.0 or
    /// Application Default Credentials with <c>aiplatform.*</c> IAM
    /// permissions, and has no API-key mode, so an
    /// <see cref="ApiKeyProviderCredential"/> is always denied before any
    /// request is sent.
    /// </summary>
    public static ProviderAuthorizationScheme AuthorizationScheme { get; } = ProviderAuthorizationScheme.OAuthTokenOnly;

    /// <summary>Gets the stable <see cref="ApiFamilyId"/> for Vertex's native generateContent wire format.</summary>
    public static ApiFamilyId ApiFamily { get; } = new("google-vertex-ai-generate-content");

    /// <summary>Gets the stable <see cref="ApiFamilyId"/> for Vertex's generic predict wire format used by text-embedding models.</summary>
    public static ApiFamilyId EmbeddingApiFamily { get; } = new("google-vertex-ai-predict-embedding");

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

    /// <summary>
    /// Gets the default capability set applied to a registered Vertex AI
    /// text-embedding model unless the caller supplies its own.
    /// </summary>
    /// <remarks>
    /// Vertex's <c>:predict</c> text-embedding instance schema accepts a
    /// <c>task_type</c> and a caller-selectable <c>outputDimensionality</c>,
    /// but always returns floating-point vectors, so encoding selection is
    /// not meaningful. Per-request input limits vary sharply by model
    /// (some models accept only a single input per request); a caller
    /// registering a specific model supplies its own
    /// <see cref="EmbeddingLimits"/> rather than relying on this shared,
    /// unbounded default.
    /// </remarks>
    public static EmbeddingCapabilities DefaultEmbeddingCapabilities { get; } = new(
        supportsBatchInput: true,
        supportsDimensions: true,
        supportsPurpose: true,
        supportsEncodingSelection: false,
        supportsTruncationControl: true,
        ExtensionData.Empty);

    /// <summary>
    /// Gets the default, unbounded embedding limits applied to a registered
    /// Vertex AI text-embedding model unless the caller supplies its own.
    /// </summary>
    public static EmbeddingLimits DefaultEmbeddingLimits { get; } =
        new(maxInputsPerRequest: null, maxInputTokensPerInput: null, defaultDimensions: null, maxDimensions: null);

    /// <summary>
    /// Gets the location value that selects Vertex AI's global endpoint,
    /// <c>global</c>, which Google recommends for current Gemini models
    /// because it offers higher availability than a single region.
    /// </summary>
    public const string GlobalLocation = "global";

    /// <summary>
    /// Gets the service base address of Vertex AI's global endpoint. Google
    /// documents that requests for <c>locations/global</c> are sent to
    /// <c>https://aiplatform.googleapis.com/</c> rather than to a
    /// <c>{location}-aiplatform.googleapis.com</c> host, so the regional
    /// host pattern must not be applied to <see cref="GlobalLocation"/>.
    /// </summary>
    public static Uri GlobalBaseAddress { get; } = new("https://aiplatform.googleapis.com/");

    /// <summary>Gets the regional base address for the given region, such as <c>us-central1</c>.</summary>
    /// <param name="location">
    /// The Google Cloud region hosting the request. Passing
    /// <see cref="GlobalLocation"/> here produces a host that does not exist;
    /// use <see cref="BuildDefaultBaseAddress"/> to resolve any location.
    /// </param>
    /// <returns>The regional Vertex AI REST base address, <c>https://{location}-aiplatform.googleapis.com/</c>.</returns>
    /// <exception cref="ArgumentException"><paramref name="location"/> is null, empty, or consists only of whitespace.</exception>
    public static Uri BuildRegionalBaseAddress(string location)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(location);

        return new Uri($"https://{location}-aiplatform.googleapis.com/");
    }

    /// <summary>
    /// Builds the service base address Google documents for the given
    /// location: <see cref="GlobalBaseAddress"/> for
    /// <see cref="GlobalLocation"/>, otherwise the regional host from
    /// <see cref="BuildRegionalBaseAddress"/>.
    /// </summary>
    /// <param name="location">
    /// The Google Cloud location hosting the request. The <c>global</c>
    /// comparison is ordinal and case-insensitive, matching how the value
    /// is typically configured.
    /// </param>
    /// <returns>The default Vertex AI REST base address for <paramref name="location"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="location"/> is null, empty, or consists only of whitespace.</exception>
    public static Uri BuildDefaultBaseAddress(string location)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(location);

        return string.Equals(location, GlobalLocation, StringComparison.OrdinalIgnoreCase)
            ? GlobalBaseAddress
            : BuildRegionalBaseAddress(location);
    }

    /// <summary>
    /// Resolves the base address a request should use: the caller's
    /// explicit <see cref="GoogleVertexAIProviderOptions.BaseAddress"/> when
    /// set, otherwise the documented default for
    /// <see cref="GoogleVertexAIProviderOptions.Location"/>.
    /// </summary>
    /// <param name="options">The validated Vertex AI provider options.</param>
    /// <returns>The absolute base address to build operation URIs against.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <see cref="GoogleVertexAIProviderOptions.BaseAddress"/> is set but not
    /// absolute, or it is unset and
    /// <see cref="GoogleVertexAIProviderOptions.Location"/> is null, empty, or
    /// whitespace.
    /// </exception>
    public static Uri ResolveBaseAddress(GoogleVertexAIProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.BaseAddress is { } explicitBaseAddress)
        {
            ArgumentException.ThrowIfNotAbsoluteUri(explicitBaseAddress, $"{nameof(options)}.{nameof(options.BaseAddress)}");
            return explicitBaseAddress;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(options.Location, $"{nameof(options)}.{nameof(options.Location)}");
        return BuildDefaultBaseAddress(options.Location);
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
    /// <returns>
    /// The absolute URI of the requested operation, rooted at
    /// <see cref="ResolveBaseAddress"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="options"/> has no project or location, or its
    /// <see cref="GoogleVertexAIProviderOptions.BaseAddress"/> is not absolute.
    /// </exception>
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
        var baseAddress = ResolveBaseAddress(options);
        var uri = new Uri(baseAddress, $"{options.ApiVersion}/{resource}:{operation}");

        return useStreaming ? new Uri($"{uri}?alt=sse") : uri;
    }

    /// <summary>
    /// Builds the absolute generic <c>:predict</c> operation URI for the
    /// given options, model, and optional deployed-endpoint override.
    /// </summary>
    /// <param name="options">The validated Vertex AI provider options.</param>
    /// <param name="modelId">The publisher model identifier to embed in the operation path.</param>
    /// <param name="deploymentId">
    /// When set, the ID of a deployed/custom Vertex AI endpoint to call
    /// instead of the shared publisher-model resource.
    /// </param>
    /// <returns>
    /// The absolute URI of the predict operation, rooted at
    /// <see cref="ResolveBaseAddress"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="options"/> has no project or location, or its
    /// <see cref="GoogleVertexAIProviderOptions.BaseAddress"/> is not absolute.
    /// </exception>
    public static Uri BuildPredictUri(GoogleVertexAIProviderOptions options, ModelId modelId, DeploymentId? deploymentId)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ProjectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Location);

        var resource = deploymentId is { } deployment
            ? $"projects/{options.ProjectId}/locations/{options.Location}/endpoints/{deployment.Value}"
            : $"projects/{options.ProjectId}/locations/{options.Location}/publishers/{options.Publisher}/models/{modelId.Value}";

        var baseAddress = ResolveBaseAddress(options);
        return new Uri(baseAddress, $"{options.ApiVersion}/{resource}:predict");
    }
}
