// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Anthropic;

using System.Diagnostics;
using System.Net.Http;

using AgentKit.Providers.Anthropic.Wire;
using AgentKit.Providers.Http;

/// <summary>
/// The Anthropic Claude conversational <see cref="ILlmModel"/>, performing
/// translation, credential resolution, transport, and stream parsing for
/// the Anthropic Messages API directly.
/// </summary>
/// <remarks>
/// Unlike the OpenAI-compatible family, Anthropic has exactly one AgentKit
/// integration today, so this class implements <see cref="ILlmModel"/>
/// directly rather than deriving from a shared protocol-family base class.
/// <see cref="IAnthropicMessageTranslator"/> and
/// <see cref="IAnthropicMessageStreamParser"/> remain independent, publicly
/// injectable collaborators so a future platform broker that must reuse
/// canonical Claude message semantics under its own authentication and
/// transport (for example, Amazon Bedrock or Google Vertex AI, per
/// Anthropic's own cross-platform guidance) can reuse them without
/// depending on this class's direct-HTTP transport.
/// </remarks>
public sealed class AnthropicLlmModel: ILlmModel
{
    /// <summary>The response header Anthropic uses to return its request identifier.</summary>
    private const string _requestIdHeaderName = "request-id";

    private readonly ModelDescriptor _descriptor;
    private readonly AnthropicProviderOptions _options;
    private readonly IAnthropicMessageTranslator _translator;
    private readonly IAnthropicMessageStreamParser _streamParser;
    private readonly IProviderCredentialSource _credentials;
    private readonly HttpClient _httpClient;
    private readonly TimeProvider _timeProvider;

    /// <summary>Initializes a new instance of the <see cref="AnthropicLlmModel"/> class.</summary>
    /// <param name="descriptor">The descriptor of the model this instance serves.</param>
    /// <param name="options">The validated Anthropic provider options.</param>
    /// <param name="translator">Translates provider-neutral requests into Anthropic Messages request bodies.</param>
    /// <param name="streamParser">Parses Anthropic Messages responses into normalized events.</param>
    /// <param name="credentials">Resolves the current credential for <paramref name="descriptor"/>'s provider.</param>
    /// <param name="httpClient">The HTTP client used to send requests.</param>
    /// <param name="timeProvider">The clock used for deadline and credential-expiry evaluation.</param>
    /// <exception cref="ArgumentNullException">Any parameter is null.</exception>
    public AnthropicLlmModel(
        ModelDescriptor descriptor,
        AnthropicProviderOptions options,
        IAnthropicMessageTranslator translator,
        IAnthropicMessageStreamParser streamParser,
        IProviderCredentialSource credentials,
        HttpClient httpClient,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(translator);
        ArgumentNullException.ThrowIfNull(streamParser);
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(timeProvider);

        Alias = descriptor.Alias;
        _descriptor = descriptor;
        _options = options;
        _translator = translator;
        _streamParser = streamParser;
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

        var authorization = ProviderAuthorizationHeaderFactory.Create(
            credential,
            _descriptor.ProviderId,
            _timeProvider,
            AnthropicProviderDefaults.AuthorizationScheme);
        if (authorization is ProviderAuthorizationDenied denied)
        {
            return await FailAsync(denied.Failure).ConfigureAwait(false);
        }

        var granted = (ProviderAuthorizationGranted) authorization;
        var useStreaming = _options.PreferStreaming && _descriptor.Capabilities.SupportsStreaming;

        JsonObject payload;
        try
        {
            payload = _translator.Translate(request, _options, useStreaming);
        }
        catch (NotSupportedException exception)
        {
            return await FailWithKindAsync(
                ProviderFailureKind.InvalidRequest,
                "The request could not be translated for the Anthropic Messages wire format.",
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
                catch (OperationCanceledException exception)
                {
                    return await FailAsync(BuildInterruptedHttpFailure(response, ProviderFailureKind.Timeout, "The transport timed out while the provider error response was being received.", exception)).ConfigureAwait(false);
                }

                return await FailAsync(failure).ConfigureAwait(false);
            }

            var parseContext = new AnthropicResponseParseContext(
                requestId,
                _descriptor.ProviderId,
                _descriptor.ApiFamily,
                _descriptor.ModelId,
                deploymentId: null,
                ProviderRequestIdReader.TryRead(response.Headers, _requestIdHeaderName));

            try
            {
                var body = await response.Content.ReadAsStreamAsync(linkedSource.Token).ConfigureAwait(false);
                await using (body.ConfigureAwait(false))
                {
                    return useStreaming
                        ? await _streamParser
                            .ParseStreamingAsync(body, parseContext, observer, linkedSource.Token)
                            .ConfigureAwait(false)
                        : await _streamParser
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

    private HttpRequestMessage CreateHttpRequest(JsonObject payload, ProviderAuthorizationGranted authorization)
    {
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, AnthropicProviderDefaults.BuildMessagesUri(_options))
        {
            Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json"),
        };

        authorization.Apply(httpRequest.Headers);
        _ = httpRequest.Headers.TryAddWithoutValidation("anthropic-version", _options.AnthropicVersion);

        return httpRequest;
    }

    private async Task<ProviderFailure> BuildHttpFailureAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        string? errorType = null;
        Exception? diagnosticCause = null;

        try
        {
            var body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using (body.ConfigureAwait(false))
            {
                var envelope = await JsonSerializer
                    .DeserializeAsync<AnthropicErrorEnvelopeDto>(body, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                errorType = envelope?.Error?.Type;
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

        var mappedBodyKind = AnthropicErrorMapping.MapErrorType(errorType);
        var kind = mappedBodyKind == ProviderFailureKind.Unknown ? HttpStatusFailureKindMapper.Map(response.StatusCode) : mappedBodyKind;

        return new ProviderFailure(
            kind,
            _descriptor.ProviderId,
            ProviderRequestIdReader.TryRead(response.Headers, _requestIdHeaderName),
            (int) response.StatusCode,
            errorType,
            RetryAfterResolver.Resolve(response.Headers, _timeProvider),
            $"The Anthropic request failed with HTTP status {(int) response.StatusCode}.",
            diagnosticCause,
            ExtensionData.Empty);
    }

    /// <summary>Builds an interrupted error-body failure while preserving response evidence already received.</summary>
    /// <param name="response">The response whose headers were received before interruption.</param>
    /// <param name="kind">The normalized interruption classification.</param>
    /// <param name="safeMessage">The bounded message safe for ordinary application handling.</param>
    /// <param name="diagnosticCause">The classified exception that interrupted response-body processing.</param>
    /// <returns>A failure retaining the raw HTTP status and Anthropic request identity.</returns>
    private ProviderFailure BuildInterruptedHttpFailure(HttpResponseMessage response, ProviderFailureKind kind, string safeMessage, Exception? diagnosticCause)
    {
        Debug.Assert(response is not null, "The error response must have been received before its body read can be interrupted.");
        Debug.Assert(Enum.IsDefined(kind), "The interruption kind must be a defined provider failure kind.");
        Debug.Assert(!string.IsNullOrWhiteSpace(safeMessage), "The interrupted failure must have a bounded safe message.");

        return new ProviderFailure(
            kind,
            _descriptor.ProviderId,
            ProviderRequestIdReader.TryRead(response.Headers, _requestIdHeaderName),
            (int) response.StatusCode,
            providerCode: null,
            RetryAfterResolver.Resolve(response.Headers, _timeProvider),
            safeMessage,
            diagnosticCause,
            ExtensionData.Empty);
    }
}
