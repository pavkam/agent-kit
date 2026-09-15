// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Cohere;

using System.Net.Http;

using AgentKit.Providers.Cohere.Wire;

/// <summary>
/// The Cohere conversational <see cref="ILlmModel"/>, performing
/// translation, credential resolution, transport, and response parsing for
/// the Cohere v2 Chat API directly.
/// </summary>
/// <remarks>
/// Like Anthropic, Gemini, and Mistral AI, Cohere has exactly one AgentKit
/// integration today, so this class implements <see cref="ILlmModel"/>
/// directly rather than deriving from a shared protocol-family base class.
/// Cohere's response shape (no <c>choices[]</c> array, a single
/// <c>message</c> object) and its semantic streaming event grammar are
/// both structurally incompatible with the OpenAI-compatible family, so it
/// uses its own <see cref="ICohereRequestTranslator"/> and
/// <see cref="ICohereResponseParser"/>.
/// </remarks>
public sealed class CohereLlmModel: ILlmModel
{
    private readonly ModelDescriptor _descriptor;
    private readonly CohereProviderOptions _options;
    private readonly ICohereRequestTranslator _translator;
    private readonly ICohereResponseParser _responseParser;
    private readonly IProviderCredentialSource _credentials;
    private readonly HttpClient _httpClient;
    private readonly TimeProvider _timeProvider;

    /// <summary>Initializes a new instance of the <see cref="CohereLlmModel"/> class.</summary>
    /// <param name="descriptor">The descriptor of the model this instance serves.</param>
    /// <param name="options">The validated Cohere provider options.</param>
    /// <param name="translator">Translates provider-neutral requests into Cohere v2 Chat request bodies.</param>
    /// <param name="responseParser">Parses Cohere v2 Chat responses into normalized events.</param>
    /// <param name="credentials">Resolves the current credential for <paramref name="descriptor"/>'s provider.</param>
    /// <param name="httpClient">The HTTP client used to send requests.</param>
    /// <param name="timeProvider">The clock used for deadline and credential-expiry evaluation.</param>
    /// <exception cref="ArgumentNullException">Any parameter is null.</exception>
    public CohereLlmModel(
        ModelDescriptor descriptor,
        CohereProviderOptions options,
        ICohereRequestTranslator translator,
        ICohereResponseParser responseParser,
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
        // sees exactly one ModelResponseStarted, contiguous sequences, and nothing after a terminal event.
        observer = new SequencingModelResponseObserver(observer);
        var requestId = request.Context.ModelRequestId;
        long sequence = 0;

        async Task<ModelAttemptResult> FailAsync(ProviderFailure failure)
        {
            await observer.OnEventAsync(new ModelResponseFailed(requestId, sequence++, failure, [], usage: null), cancellationToken)
                .ConfigureAwait(false);
            return new ModelAttemptFailed(failure, [], usage: null);
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

        async Task<ModelAttemptResult> CancelAsync()
        {
            var cancellation = new ProviderFailure(
                ProviderFailureKind.Cancellation,
                _descriptor.ProviderId,
                requestId: null,
                statusCode: null,
                providerCode: null,
                retryAfter: null,
                "The attempt was cancelled.",
                diagnosticCause: null,
                ExtensionData.Empty);

            await observer.OnEventAsync(new ModelResponseCancelled(requestId, sequence++, cancellation, [], usage: null), cancellationToken)
                .ConfigureAwait(false);
            return new ModelAttemptCancelled(cancellation, [], usage: null);
        }

        await observer.OnEventAsync(new ModelResponseStarted(requestId, sequence++), cancellationToken).ConfigureAwait(false);

        if (request.Context.Tools.Length > 0 && !_descriptor.Capabilities.SupportsToolCalls)
        {
            return await FailWithKindAsync(
                ProviderFailureKind.InvalidRequest,
                "The selected model does not support tool calls.").ConfigureAwait(false);
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

        var authorization = CohereAuthorizationHeaderFactory.Create(credential, _descriptor.ProviderId, _timeProvider);
        if (authorization is CohereAuthorizationDenied denied)
        {
            return await FailAsync(denied.Failure).ConfigureAwait(false);
        }

        var granted = (CohereAuthorizationGranted) authorization;
        var useStreaming = _options.PreferStreaming && _descriptor.Capabilities.SupportsStreaming;

        JsonObject payload;
        try
        {
            payload = _translator.Translate(request, useStreaming);
        }
        catch (NotSupportedException exception)
        {
            return await FailWithKindAsync(
                ProviderFailureKind.InvalidRequest,
                "The request could not be translated for the Cohere v2 Chat wire format.",
                exception).ConfigureAwait(false);
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
            return await CancelAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException exception) when (deadlineSource.IsCancellationRequested)
        {
            return await FailWithKindAsync(
                ProviderFailureKind.Timeout,
                "The request did not complete before its deadline.",
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
                return await FailAsync(await BuildHttpFailureAsync(response, linkedSource.Token).ConfigureAwait(false))
                    .ConfigureAwait(false);
            }

            var parseContext = new CohereResponseParseContext(
                requestId,
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
                    return useStreaming
                        ? await _responseParser
                            .ParseStreamingAsync(body, parseContext, observer, linkedSource.Token)
                            .ConfigureAwait(false)
                        : await _responseParser
                            .ParseBufferedAsync(body, parseContext, observer, linkedSource.Token)
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
        }
    }

    private HttpRequestMessage CreateHttpRequest(JsonObject payload, CohereAuthorizationGranted authorization)
    {
        var uri = CohereProviderDefaults.BuildChatUri(_options);
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json"),
        };

        _ = httpRequest.Headers.TryAddWithoutValidation(authorization.HeaderName, authorization.HeaderValue);
        if (_options.ClientName is { Length: > 0 } clientName)
        {
            _ = httpRequest.Headers.TryAddWithoutValidation("X-Client-Name", clientName);
        }

        return httpRequest;
    }

    private async Task<ProviderFailure> BuildHttpFailureAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        string? safeMessage = null;

        try
        {
            var body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using (body.ConfigureAwait(false))
            {
                var envelope = await JsonSerializer
                    .DeserializeAsync<CohereErrorEnvelopeDto>(body, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                safeMessage = envelope?.Message;
            }
        }
        catch (JsonException)
        {
            // The error body was not valid JSON; fall back to a generic message below.
        }

        return new ProviderFailure(
            CohereErrorMapping.MapStatusCode(response.StatusCode),
            _descriptor.ProviderId,
            requestId: null,
            (int) response.StatusCode,
            providerCode: null,
            response.Headers.RetryAfter?.Delta,
            safeMessage ?? $"The provider returned HTTP status {(int) response.StatusCode}.",
            diagnosticCause: null,
            ExtensionData.Empty);
    }
}
