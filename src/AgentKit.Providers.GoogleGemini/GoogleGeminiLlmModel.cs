// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleGemini;

using System.Net.Http;

using AgentKit.Providers;
using AgentKit.Providers.Egress;

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
    private readonly ProviderEgress _egress;
    private readonly TimeProvider _timeProvider;
    private readonly IProviderProfileRuntimeSelector _profileSelector;

    /// <summary>Initializes a new instance of the <see cref="GoogleGeminiLlmModel"/> class.</summary>
    /// <param name="descriptor">The descriptor of the model this instance serves.</param>
    /// <param name="options">The validated Gemini provider options.</param>
    /// <param name="translator">Translates provider-neutral requests into Gemini GenerateContent request bodies.</param>
    /// <param name="responseParser">Parses Gemini GenerateContent responses into normalized events.</param>
    /// <param name="egress">The provider-egress boundary every attempt sends through.</param>
    /// <param name="timeProvider">The clock used for deadline evaluation.</param>
    /// <param name="profileSelector">The engine-wide profile runtime selector that resolves the descriptor's captured endpoint and credential profile binding.</param>
    /// <exception cref="ArgumentNullException">Any parameter is null.</exception>
    public GoogleGeminiLlmModel(
        ModelDescriptor descriptor,
        GoogleGeminiProviderOptions options,
        IGoogleGeminiContentTranslator translator,
        IGoogleGeminiResponseParser responseParser,
        ProviderEgress egress,
        TimeProvider timeProvider,
        IProviderProfileRuntimeSelector profileSelector)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(translator);
        ArgumentNullException.ThrowIfNull(responseParser);
        ArgumentNullException.ThrowIfNull(egress);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(profileSelector);

        Alias = descriptor.Alias;
        _descriptor = descriptor;
        _options = options;
        _translator = translator;
        _responseParser = responseParser;
        _egress = egress;
        _timeProvider = timeProvider;
        _profileSelector = profileSelector;
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

        await using var sendContext = await NativeProviderChatSend.BeginAsync(
                _descriptor,
                request,
                _profileSelector,
                _timeProvider,
                cancellationToken)
            .ConfigureAwait(false);
        if (!sendContext.IsSuccess)
        {
            return sendContext.Failure!.Kind is ProviderFailureKind.Cancellation
                ? await CancelAsync(sendContext.Failure).ConfigureAwait(false)
                : await FailAsync(sendContext.Failure).ConfigureAwait(false);
        }

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

        using var httpRequest = CreateHttpRequest(payload, useStreaming, sendContext.EndpointBaseOverride);
        using var deadlineSource = new CancellationTokenSource(remaining, _timeProvider);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadlineSource.Token);

        var sent = await _egress
            .SendAsync(ProviderEgressRequest.ForConversation(_descriptor, request, httpRequest, useStreaming, sendContext.CreateCredential(GoogleGeminiProviderDefaults.AuthorizationScheme)), cancellationToken)
            .ConfigureAwait(false);
        if (sent is ProviderEgressRefused refused)
        {
            return refused.Failure.Kind is ProviderFailureKind.Cancellation
                ? await CancelAsync(refused.Failure).ConfigureAwait(false)
                : await FailAsync(refused.Failure).ConfigureAwait(false);
        }

        var response = ((ProviderEgressSent) sent).Response;

        await using (response.ConfigureAwait(false))
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
                deploymentId: null,
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
                    ProviderEgressBodyFault.Classify(exception, out var safeMessage),
                    safeMessage,
                    exception).ConfigureAwait(false);
            }
        }
    }

    private HttpRequestMessage CreateHttpRequest(
        JsonObject payload,
        bool useStreaming,
        Uri? endpointBaseOverride)
    {
        var uri = endpointBaseOverride is null
            ? GoogleGeminiProviderDefaults.BuildGenerateContentUri(_options, _descriptor.ModelId, useStreaming)
            : GoogleGeminiProviderDefaults.BuildGenerateContentUri(
                new GoogleGeminiProviderOptions
                {
                    BaseAddress = endpointBaseOverride,
                    ApiVersion = _options.ApiVersion,
                    PreferStreaming = _options.PreferStreaming,
                },
                _descriptor.ModelId,
                useStreaming);
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json"),
        };


        return httpRequest;
    }
}
