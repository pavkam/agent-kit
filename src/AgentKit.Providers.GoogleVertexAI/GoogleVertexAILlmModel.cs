// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleVertexAI;

using System.Net.Http;

using AgentKit.Providers.Http;

/// <summary>
/// The Google Vertex AI conversational <see cref="ILlmModel"/>, performing
/// Vertex-specific credential resolution, resource-qualified endpoint
/// construction, and transport, while delegating request-body translation
/// and response parsing to the shared
/// <see cref="IGoogleGeminiContentTranslator"/> and
/// <see cref="IGoogleGeminiResponseParser"/> collaborators from
/// <c>AgentKit.Providers.GoogleGemini</c>.
/// </summary>
/// <remarks>
/// This class does not derive from <see cref="GoogleGeminiLlmModel"/>
/// because Vertex AI differs in the dimensions that class hardcodes:
/// authentication (OAuth-only, no API-key mode) and the request URL (a
/// project/location/publisher-qualified resource, or a deployed-endpoint
/// resource, on a regional, global, or caller-supplied host rather than
/// the Developer API's flat model path). The
/// <c>GenerateContentRequest</c>/<c>GenerateContentResponse</c> wire body
/// and the <c>google.rpc.Status</c> error envelope are unchanged, which is
/// exactly what <see cref="IGoogleGeminiContentTranslator"/>,
/// <see cref="IGoogleGeminiResponseParser"/>, and
/// <see cref="GoogleApiErrorFailureFactory"/> were designed to let a second
/// host reuse without depending on <see cref="GoogleGeminiLlmModel"/>'s
/// direct-HTTP transport.
/// </remarks>
public sealed class GoogleVertexAILlmModel: ILlmModel
{
    private readonly ModelDescriptor _descriptor;
    private readonly GoogleVertexAIProviderOptions _options;
    private readonly IGoogleGeminiContentTranslator _translator;
    private readonly IGoogleGeminiResponseParser _responseParser;
    private readonly IProviderCredentialSource _credentials;
    private readonly HttpClient _httpClient;
    private readonly TimeProvider _timeProvider;

    /// <summary>Initializes a new instance of the <see cref="GoogleVertexAILlmModel"/> class.</summary>
    /// <param name="descriptor">
    /// The descriptor of the model this instance serves; when its
    /// <see cref="ModelDescriptor.DeploymentId"/> is set, requests target
    /// that deployed/custom Vertex AI endpoint instead of the shared
    /// publisher-model resource.
    /// </param>
    /// <param name="options">The validated Vertex AI provider options.</param>
    /// <param name="translator">Translates provider-neutral requests into Gemini GenerateContent request bodies.</param>
    /// <param name="responseParser">Parses Gemini GenerateContent responses into normalized events.</param>
    /// <param name="credentials">Resolves the current credential for <paramref name="descriptor"/>'s provider.</param>
    /// <param name="httpClient">The HTTP client used to send requests.</param>
    /// <param name="timeProvider">The clock used for deadline and credential-expiry evaluation.</param>
    /// <exception cref="ArgumentNullException">Any parameter is null.</exception>
    public GoogleVertexAILlmModel(
        ModelDescriptor descriptor,
        GoogleVertexAIProviderOptions options,
        IGoogleGeminiContentTranslator translator,
        IGoogleGeminiResponseParser responseParser,
        IProviderCredentialSource credentials,
        HttpClient httpClient,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(translator);
        ArgumentNullException.ThrowIfNull(responseParser);
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(timeProvider);

        Alias = descriptor.Alias;
        _descriptor = descriptor;
        _options = options;
        _translator = translator;
        _responseParser = responseParser;
        _credentials = credentials;
        _httpClient = httpClient;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc/>
    public ModelAlias Alias { get; }

    /// <inheritdoc/>
    public async Task<ModelAttemptResult> ExecuteAsync(
        LlmModelRequest request,
        IModelResponseObserver observer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(observer);

        // This adapter and its wire parser both emit events; the sequencing wrapper guarantees the observer
        // sees exactly one ModelResponseStarted, contiguous sequences, and nothing after a terminal event. It
        // also retains the parts already completed and the latest usage, so an attempt interrupted outside the
        // parser (transport fault, deadline, caller cancellation) still settles with truthful partial output.
        var sequencing = new SequencingModelResponseObserver(observer);
        var requestId = request.Context.ModelRequestId;

        async Task<ModelAttemptResult> FailAsync(ProviderFailure failure)
        {
            var partialParts = sequencing.CompletedParts;
            var usage = sequencing.Usage;
            await sequencing.OnEventAsync(
                    new ModelResponseFailed(requestId, sequencing.NextSequence, failure, partialParts, usage),
                    cancellationToken)
                .ConfigureAwait(false);
            return new ModelAttemptFailed(failure, partialParts, usage);
        }

        Task<ModelAttemptResult> FailWithKindAsync(ProviderFailureKind kind, string safeMessage, Exception? cause = null) =>
            FailAsync(new ProviderFailure(
                kind,
                _descriptor.ProviderId,
                requestId: null,
                statusCode: null,
                providerCode: null,
                retryAfter: null,
                safeMessage,
                cause,
                ExtensionData.Empty));

        async Task<ModelAttemptResult> CancelAsync(ProviderFailure? knownFailure = null)
        {
            var cancellation = knownFailure ?? new ProviderFailure(
                ProviderFailureKind.Cancellation,
                _descriptor.ProviderId,
                requestId: null,
                statusCode: null,
                providerCode: null,
                retryAfter: null,
                "The attempt was cancelled.",
                diagnosticCause: null,
                ExtensionData.Empty);

            // The caller's token is usually the reason the attempt is being cancelled; delivering the terminal
            // event with it would let a token-honouring observer throw and lose the cancellation record.
            var partialParts = sequencing.CompletedParts;
            var usage = sequencing.Usage;
            await sequencing.OnEventAsync(
                    new ModelResponseCancelled(requestId, sequencing.NextSequence, cancellation, partialParts, usage),
                    CancellationToken.None)
                .ConfigureAwait(false);
            return new ModelAttemptCancelled(cancellation, partialParts, usage);
        }

        await sequencing.OnEventAsync(new ModelResponseStarted(requestId, sequencing.NextSequence), cancellationToken).ConfigureAwait(false);

        if (ModelRequestPreflight.Validate(request, _descriptor) is { } preflightFailure)
        {
            return await FailAsync(preflightFailure).ConfigureAwait(false);
        }

        var remaining = request.Deadline - _timeProvider.GetUtcNow();
        if (remaining <= TimeSpan.Zero)
        {
            return await FailWithKindAsync(
                ProviderFailureKind.Timeout,
                "The request deadline had already elapsed before the attempt could be sent.").ConfigureAwait(false);
        }

        ProviderCredential credential;
        try
        {
            credential = await _credentials.GetCredentialAsync(_descriptor.ProviderId, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return await CancelAsync().ConfigureAwait(false);
        }

        var authorization = ProviderAuthorizationHeaderFactory.Create(
            credential,
            _descriptor.ProviderId,
            _timeProvider,
            GoogleVertexAIProviderDefaults.AuthorizationScheme);
        if (authorization is ProviderAuthorizationDenied denied)
        {
            return await FailAsync(denied.Failure).ConfigureAwait(false);
        }

        var granted = (ProviderAuthorizationGranted) authorization;
        var useStreaming = _options.PreferStreaming && _descriptor.Capabilities.SupportsStreaming;

        JsonObject payload;
        try
        {
            payload = _translator.Translate(request);
        }
        catch (NotSupportedException exception)
        {
            return await FailWithKindAsync(
                ProviderFailureKind.InvalidRequest,
                "The request could not be translated for the Vertex AI generateContent wire format.",
                exception).ConfigureAwait(false);
        }

        using var httpRequest = CreateHttpRequest(payload, granted, useStreaming);
        using var deadlineSource = new CancellationTokenSource(remaining, _timeProvider);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadlineSource.Token);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient
                .SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, linkedSource.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return await CancelAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException exception) when (deadlineSource.IsCancellationRequested)
        {
            return await FailWithKindAsync(
                ProviderFailureKind.Timeout,
                "The request did not complete before its deadline.",
                exception).ConfigureAwait(false);
        }
        catch (OperationCanceledException exception)
        {
            // Neither the caller nor the request deadline cancelled: this is the transport's own timeout
            // (HttpClient.Timeout surfaces as TaskCanceledException). It is a typed timeout, never a caller cancellation.
            return await FailWithKindAsync(
                ProviderFailureKind.Timeout,
                "The transport timed out before the provider responded.",
                exception).ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            return await FailWithKindAsync(
                ProviderFailureKind.Unavailable,
                "The provider could not be reached.",
                exception).ConfigureAwait(false);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                ProviderFailure failure;
                try
                {
                    failure = await GoogleApiErrorFailureFactory.CreateAsync(response, _descriptor.ProviderId, _timeProvider, linkedSource.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
                {
                    return await CancelAsync(GoogleApiErrorFailureFactory.CreateInterrupted(response, _descriptor.ProviderId, ProviderFailureKind.Cancellation, "The attempt was cancelled while receiving the provider error response.", exception, _timeProvider)).ConfigureAwait(false);
                }
                catch (OperationCanceledException exception) when (deadlineSource.IsCancellationRequested)
                {
                    return await FailAsync(GoogleApiErrorFailureFactory.CreateInterrupted(response, _descriptor.ProviderId, ProviderFailureKind.Timeout, "The provider error response was not received before the request deadline.", exception, _timeProvider)).ConfigureAwait(false);
                }
                catch (OperationCanceledException exception)
                {
                    return await FailAsync(GoogleApiErrorFailureFactory.CreateInterrupted(response, _descriptor.ProviderId, ProviderFailureKind.Timeout, "The transport timed out while the provider error response was being received.", exception, _timeProvider)).ConfigureAwait(false);
                }

                return await FailAsync(failure).ConfigureAwait(false);
            }

            var parseContext = new ProviderResponseParseContext(
                requestId,
                _descriptor.ProviderId,
                _descriptor.ApiFamily,
                _descriptor.ModelId,
                _descriptor.DeploymentId,
                providerRequestId: null);

            try
            {
                var body = await response.Content.ReadAsStreamAsync(linkedSource.Token).ConfigureAwait(false);
                await using (body.ConfigureAwait(false))
                {
                    return useStreaming
                        ? await _responseParser
                            .ParseStreamingAsync(body, parseContext, sequencing, linkedSource.Token)
                            .ConfigureAwait(false)
                        : await _responseParser
                            .ParseBufferedAsync(body, parseContext, sequencing, linkedSource.Token)
                            .ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return await CancelAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException exception) when (deadlineSource.IsCancellationRequested)
            {
                return await FailWithKindAsync(
                    ProviderFailureKind.Timeout,
                    "The response was not fully received before the request's deadline.",
                    exception).ConfigureAwait(false);
            }
            catch (OperationCanceledException exception)
            {
                return await FailWithKindAsync(
                    ProviderFailureKind.Timeout,
                    "The transport timed out while the response body was being received.",
                    exception).ConfigureAwait(false);
            }
            catch (IOException exception)
            {
                // A connection reset or truncated body mid-stream is a transport failure, not a caller fault.
                return await FailWithKindAsync(
                    ProviderFailureKind.Unavailable,
                    "The connection failed while the response body was being received.",
                    exception).ConfigureAwait(false);
            }
        }
    }

    private HttpRequestMessage CreateHttpRequest(JsonObject payload, ProviderAuthorizationGranted authorization, bool useStreaming)
    {
        var uri = GoogleVertexAIProviderDefaults.BuildGenerateContentUri(_options, _descriptor.ModelId, _descriptor.DeploymentId, useStreaming);
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json"),
        };

        authorization.Apply(httpRequest.Headers);

        return httpRequest;
    }
}
