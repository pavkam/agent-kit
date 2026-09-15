// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleGemini;

using System.Diagnostics;
using System.Net;
using System.Net.Http;

using AgentKit.Providers.GoogleGemini.Wire;

/// <summary>
/// The Google Gemini conversational <see cref="ILlmModel"/>, performing
/// translation, credential resolution, transport, and stream parsing for
/// the Gemini GenerateContent API directly.
/// </summary>
/// <remarks>
/// Like Anthropic, Gemini has exactly one AgentKit integration today, so
/// this class implements <see cref="ILlmModel"/> directly rather than
/// deriving from a shared protocol-family base class.
/// <see cref="IGoogleGeminiContentTranslator"/> and
/// <see cref="IGoogleGeminiResponseParser"/> remain independent, publicly
/// injectable collaborators so a future Google Vertex AI integration that
/// must reuse canonical Gemini content semantics under Google Cloud IAM
/// authentication and regional resource names can reuse them without
/// depending on this class's direct-HTTP transport.
/// </remarks>
public sealed class GoogleGeminiLlmModel: ILlmModel
{
    private readonly ModelDescriptor _descriptor;
    private readonly GoogleGeminiProviderOptions _options;
    private readonly IGoogleGeminiContentTranslator _translator;
    private readonly IGoogleGeminiResponseParser _responseParser;
    private readonly IProviderCredentialSource _credentials;
    private readonly HttpClient _httpClient;
    private readonly TimeProvider _timeProvider;

    /// <summary>Initializes a new instance of the <see cref="GoogleGeminiLlmModel"/> class.</summary>
    /// <param name="descriptor">The descriptor of the model this instance serves.</param>
    /// <param name="options">The validated Gemini provider options.</param>
    /// <param name="translator">Translates provider-neutral requests into Gemini GenerateContent request bodies.</param>
    /// <param name="responseParser">Parses Gemini GenerateContent responses into normalized events.</param>
    /// <param name="credentials">Resolves the current credential for <paramref name="descriptor"/>'s provider.</param>
    /// <param name="httpClient">The HTTP client used to send requests.</param>
    /// <param name="timeProvider">The clock used for deadline and credential-expiry evaluation.</param>
    /// <exception cref="ArgumentNullException">Any parameter is null.</exception>
    public GoogleGeminiLlmModel(
        ModelDescriptor descriptor,
        GoogleGeminiProviderOptions options,
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

        var authorization = GoogleGeminiAuthorizationHeaderFactory.Create(credential, _descriptor.ProviderId, _timeProvider);
        if (authorization is GoogleGeminiAuthorizationDenied denied)
        {
            return await FailAsync(denied.Failure).ConfigureAwait(false);
        }

        var granted = (GoogleGeminiAuthorizationGranted) authorization;
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
                "The request could not be translated for the Gemini GenerateContent wire format.",
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
                    failure = await BuildHttpFailureAsync(response, linkedSource.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
                {
                    return await CancelAsync(BuildInterruptedHttpFailure(response, ProviderFailureKind.Cancellation, "The attempt was cancelled while receiving the provider error response.", exception)).ConfigureAwait(false);
                }
                catch (OperationCanceledException exception) when (deadlineSource.IsCancellationRequested)
                {
                    return await FailAsync(BuildInterruptedHttpFailure(response, ProviderFailureKind.Timeout, "The provider error response was not received before the request deadline.", exception)).ConfigureAwait(false);
                }

                return await FailAsync(failure).ConfigureAwait(false);
            }

            var parseContext = new GoogleGeminiResponseParseContext(
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

    private HttpRequestMessage CreateHttpRequest(JsonObject payload, GoogleGeminiAuthorizationGranted authorization, bool useStreaming)
    {
        var uri = GoogleGeminiProviderDefaults.BuildGenerateContentUri(_options, _descriptor.ModelId, useStreaming);
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json"),
        };

        _ = httpRequest.Headers.TryAddWithoutValidation(authorization.HeaderName, authorization.HeaderValue);

        return httpRequest;
    }

    private async Task<ProviderFailure> BuildHttpFailureAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        string? status = null;
        Exception? diagnosticCause = null;

        try
        {
            var body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using (body.ConfigureAwait(false))
            {
                var envelope = await JsonSerializer
                    .DeserializeAsync<GoogleGeminiErrorEnvelopeDto>(body, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                status = envelope?.Error?.Status;
            }
        }
        catch (JsonException exception)
        {
            diagnosticCause = exception;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            diagnosticCause = exception;
        }

        var mappedBodyKind = GoogleGeminiErrorMapping.MapStatus(status);
        var kind = mappedBodyKind == ProviderFailureKind.Unknown ? MapStatusCode(response.StatusCode) : mappedBodyKind;

        return new ProviderFailure(
            kind,
            _descriptor.ProviderId,
            requestId: null,
            (int) response.StatusCode,
            status,
            response.Headers.RetryAfter?.Delta,
            $"The Google Gemini request failed with HTTP status {(int) response.StatusCode}.",
            diagnosticCause,
            ExtensionData.Empty);
    }

    private static ProviderFailureKind MapStatusCode(HttpStatusCode statusCode) =>
        (int) statusCode switch
        {
            401 => ProviderFailureKind.Authentication,
            403 => ProviderFailureKind.Authorization,
            429 => ProviderFailureKind.Throttling,
            408 => ProviderFailureKind.Timeout,
            400 or 404 => ProviderFailureKind.InvalidRequest,
            >= 500 and <= 599 => ProviderFailureKind.Unavailable,
            >= 300 and <= 499 => ProviderFailureKind.InvalidRequest,
            >= 100 and <= 299 => ProviderFailureKind.ProtocolViolation,
            _ => ProviderFailureKind.Unknown,
        };

    /// <summary>Builds an interrupted error-body failure while preserving response evidence already received.</summary>
    /// <param name="response">The response whose headers were received before interruption.</param>
    /// <param name="kind">The normalized interruption classification.</param>
    /// <param name="safeMessage">The bounded message safe for ordinary application handling.</param>
    /// <param name="diagnosticCause">The classified exception that interrupted response-body processing.</param>
    /// <returns>A failure retaining the raw HTTP status received from Google Gemini.</returns>
    private ProviderFailure BuildInterruptedHttpFailure(HttpResponseMessage response, ProviderFailureKind kind, string safeMessage, Exception? diagnosticCause)
    {
        Debug.Assert(response is not null, "The error response must have been received before its body read can be interrupted.");
        Debug.Assert(Enum.IsDefined(kind), "The interruption kind must be a defined provider failure kind.");
        Debug.Assert(!string.IsNullOrWhiteSpace(safeMessage), "The interrupted failure must have a bounded safe message.");

        return new ProviderFailure(
            kind,
            _descriptor.ProviderId,
            requestId: null,
            (int) response.StatusCode,
            providerCode: null,
            response.Headers.RetryAfter?.Delta,
            safeMessage,
            diagnosticCause,
            ExtensionData.Empty);
    }
}
