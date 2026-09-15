// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleGemini;

using System.Net.Http;

using AgentKit.Providers.Http;

/// <summary>
/// The Google Gemini embedding <see cref="IEmbeddingModel"/>, performing
/// translation, credential resolution, transport, and response parsing for
/// the Gemini <c>batchEmbedContents</c> API directly.
/// </summary>
public sealed class GoogleGeminiEmbeddingModel: IEmbeddingModel
{
    private readonly EmbeddingModelDescriptor _descriptor;
    private readonly GoogleGeminiProviderOptions _options;
    private readonly IGoogleGeminiEmbeddingRequestTranslator _translator;
    private readonly IGoogleGeminiEmbeddingResponseParser _responseParser;
    private readonly IProviderCredentialSource _credentials;
    private readonly HttpClient _httpClient;
    private readonly TimeProvider _timeProvider;

    /// <summary>Initializes a new instance of the <see cref="GoogleGeminiEmbeddingModel"/> class.</summary>
    /// <param name="descriptor">The descriptor of the model this instance serves.</param>
    /// <param name="options">The validated Gemini provider options.</param>
    /// <param name="translator">Translates provider-neutral requests into Gemini batchEmbedContents request bodies.</param>
    /// <param name="responseParser">Parses Gemini batchEmbedContents responses into normalized results.</param>
    /// <param name="credentials">Resolves the current credential for <paramref name="descriptor"/>'s provider.</param>
    /// <param name="httpClient">The HTTP client used to send requests.</param>
    /// <param name="timeProvider">The clock used for deadline and credential-expiry evaluation.</param>
    /// <exception cref="ArgumentNullException">Any parameter is null.</exception>
    public GoogleGeminiEmbeddingModel(
        EmbeddingModelDescriptor descriptor,
        GoogleGeminiProviderOptions options,
        IGoogleGeminiEmbeddingRequestTranslator translator,
        IGoogleGeminiEmbeddingResponseParser responseParser,
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
    public EmbeddingModelAlias Alias { get; }

    /// <inheritdoc/>
    public async Task<EmbeddingAttemptResult> GenerateAsync(
        EmbeddingModelRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        EmbeddingAttemptResult FailWithKind(ProviderFailureKind kind, string safeMessage, Exception? cause = null) =>
            new EmbeddingAttemptFailed(new ProviderFailure(
                kind,
                _descriptor.ProviderId,
                requestId: null,
                statusCode: null,
                providerCode: null,
                retryAfter: null,
                safeMessage,
                cause,
                ExtensionData.Empty));

        static EmbeddingAttemptResult Cancel(ProviderId providerId) =>
            new EmbeddingAttemptCancelled(new ProviderFailure(
                ProviderFailureKind.Cancellation,
                providerId,
                requestId: null,
                statusCode: null,
                providerCode: null,
                retryAfter: null,
                "The attempt was cancelled.",
                diagnosticCause: null,
                ExtensionData.Empty));

        if (ModelRequestPreflight.Validate(request, _descriptor) is { } preflightFailure)
        {
            return new EmbeddingAttemptFailed(preflightFailure);
        }

        var remaining = request.Deadline - _timeProvider.GetUtcNow();
        if (remaining <= TimeSpan.Zero)
        {
            return FailWithKind(
                ProviderFailureKind.Timeout,
                "The request deadline had already elapsed before the attempt could be sent.");
        }

        ProviderCredential credential;
        try
        {
            credential = await _credentials.GetCredentialAsync(_descriptor.ProviderId, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Cancel(_descriptor.ProviderId);
        }

        var authorization = ProviderAuthorizationHeaderFactory.Create(
            credential,
            _descriptor.ProviderId,
            _timeProvider,
            GoogleGeminiProviderDefaults.AuthorizationScheme);
        if (authorization is ProviderAuthorizationDenied denied)
        {
            return new EmbeddingAttemptFailed(denied.Failure);
        }

        var granted = (ProviderAuthorizationGranted) authorization;

        JsonObject payload;
        try
        {
            payload = _translator.Translate(request);
        }
        catch (NotSupportedException exception)
        {
            return FailWithKind(
                ProviderFailureKind.InvalidRequest,
                "The request could not be translated for the Gemini batchEmbedContents wire format.",
                exception);
        }

        using var httpRequest = CreateHttpRequest(payload, granted);
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
            return Cancel(_descriptor.ProviderId);
        }
        catch (OperationCanceledException exception) when (deadlineSource.IsCancellationRequested)
        {
            return FailWithKind(
                ProviderFailureKind.Timeout,
                "The request did not complete before its deadline.",
                exception);
        }
        catch (OperationCanceledException exception)
        {
            // Neither the caller nor the request deadline cancelled: this is the transport's own timeout
            // (HttpClient.Timeout surfaces as TaskCanceledException). It is a typed timeout, never a caller cancellation.
            return FailWithKind(
                ProviderFailureKind.Timeout,
                "The transport timed out before the provider responded.",
                exception);
        }
        catch (HttpRequestException exception)
        {
            return FailWithKind(
                ProviderFailureKind.Unavailable,
                "The provider could not be reached.",
                exception);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                try
                {
                    return new EmbeddingAttemptFailed(await GoogleApiErrorFailureFactory.CreateAsync(response, _descriptor.ProviderId, _timeProvider, linkedSource.Token).ConfigureAwait(false));
                }
                catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
                {
                    return new EmbeddingAttemptCancelled(GoogleApiErrorFailureFactory.CreateInterrupted(response, _descriptor.ProviderId, ProviderFailureKind.Cancellation, "The attempt was cancelled while receiving the provider error response.", exception, _timeProvider));
                }
                catch (OperationCanceledException exception) when (deadlineSource.IsCancellationRequested)
                {
                    return new EmbeddingAttemptFailed(GoogleApiErrorFailureFactory.CreateInterrupted(response, _descriptor.ProviderId, ProviderFailureKind.Timeout, "The provider error response was not received before the request deadline.", exception, _timeProvider));
                }
                catch (OperationCanceledException exception)
                {
                    return new EmbeddingAttemptFailed(GoogleApiErrorFailureFactory.CreateInterrupted(response, _descriptor.ProviderId, ProviderFailureKind.Timeout, "The transport timed out while the provider error response was being received.", exception, _timeProvider));
                }
            }

            var parseContext = new EmbeddingResponseParseContext(
                request.Context.RequestId,
                _descriptor.ProviderId,
                _descriptor.ApiFamily,
                _descriptor.ModelId,
                deploymentId: null,
                providerRequestId: null);

            try
            {
                var body = await response.Content.ReadAsStreamAsync(linkedSource.Token).ConfigureAwait(false);
                await using (body.ConfigureAwait(false))
                {
                    return await _responseParser
                        .ParseAsync(body, parseContext, request.Context.Request.Inputs, linkedSource.Token)
                        .ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return Cancel(_descriptor.ProviderId);
            }
            catch (OperationCanceledException exception) when (deadlineSource.IsCancellationRequested)
            {
                return FailWithKind(
                    ProviderFailureKind.Timeout,
                    "The response was not fully received before the request's deadline.",
                    exception);
            }
            catch (OperationCanceledException exception)
            {
                return FailWithKind(
                    ProviderFailureKind.Timeout,
                    "The transport timed out while the response body was being received.",
                    exception);
            }
            catch (IOException exception)
            {
                // A connection reset or truncated body mid-stream is a transport failure, not a caller fault.
                return FailWithKind(
                    ProviderFailureKind.Unavailable,
                    "The connection failed while the response body was being received.",
                    exception);
            }
        }
    }

    private HttpRequestMessage CreateHttpRequest(JsonObject payload, ProviderAuthorizationGranted authorization)
    {
        var uri = GoogleGeminiProviderDefaults.BuildBatchEmbedContentsUri(_options, _descriptor.ModelId);
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json"),
        };

        authorization.Apply(httpRequest.Headers);

        return httpRequest;
    }
}
