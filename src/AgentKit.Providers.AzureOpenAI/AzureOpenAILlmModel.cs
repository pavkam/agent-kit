// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AzureOpenAI;

using System.Diagnostics;
using System.Net.Http;

using AgentKit.Providers.AzureOpenAI.Wire;
using AgentKit.Providers.Http;

/// <summary>
/// The Azure OpenAI conversational <see cref="ILlmModel"/>, performing
/// Azure-specific credential resolution, endpoint construction, and
/// transport, while delegating request-body translation and
/// response parsing to the shared OpenAI-compatible wire-shape
/// collaborators from <c>AgentKit.Providers.OpenAICompatible</c>.
/// </summary>
/// <remarks>
/// <para>
/// This class does not derive from <c>OpenAICompatibleLlmModelBase</c>
/// because that base class hardcodes <c>Authorization: Bearer</c>
/// authentication, which cannot represent Azure's <c>api-key</c> header
/// credential shape. Instead, it implements <see cref="ILlmModel"/>
/// directly and reuses the base class's injectable, provider-neutral
/// <see cref="IOpenAIRequestTranslator"/> and <see cref="IOpenAIStreamParser"/>
/// collaborators, which those interfaces were designed to support: the
/// request/response wire shape is genuinely OpenAI-compatible even though
/// authentication and endpoint construction are not.
/// </para>
/// <para>
/// Azure's GA v1 dialect addresses a model by Azure deployment name (via
/// <see cref="ModelDescriptor.DeploymentId"/>) rather than the underlying
/// model identity (<see cref="ModelDescriptor.ModelId"/>) alone, so after
/// translation this class overwrites the body's <c>model</c> field with
/// the deployment name whenever one is configured.
/// </para>
/// </remarks>
public sealed class AzureOpenAILlmModel: ILlmModel
{
    /// <summary>The response header Azure OpenAI uses to return its request identifier.</summary>
    private const string _requestIdHeaderName = "x-request-id";

    private readonly ModelDescriptor _descriptor;
    private readonly OpenAICompatibilityProfile _profile;
    private readonly IOpenAIRequestTranslator _translator;
    private readonly IOpenAIStreamParser _streamParser;
    private readonly IProviderCredentialSource _credentials;
    private readonly HttpClient _httpClient;
    private readonly TimeProvider _timeProvider;

    /// <summary>Initializes a new instance of the <see cref="AzureOpenAILlmModel"/> class.</summary>
    /// <param name="descriptor">
    /// The descriptor of the model this instance serves; its
    /// <see cref="ModelDescriptor.DeploymentId"/> supplies the Azure
    /// deployment name sent as the wire <c>model</c> field.
    /// </param>
    /// <param name="profile">The tested wire-behavior configuration for the target resource endpoint.</param>
    /// <param name="translator">Translates provider-neutral requests into OpenAI-compatible request bodies.</param>
    /// <param name="streamParser">Parses OpenAI-compatible responses into normalized events.</param>
    /// <param name="credentials">Resolves the current credential for <paramref name="descriptor"/>'s provider.</param>
    /// <param name="httpClient">The HTTP client used to send requests.</param>
    /// <param name="timeProvider">The clock used for deadline and credential-expiry evaluation.</param>
    /// <exception cref="ArgumentNullException">Any parameter is null.</exception>
    public AzureOpenAILlmModel(
        ModelDescriptor descriptor,
        OpenAICompatibilityProfile profile,
        IOpenAIRequestTranslator translator,
        IOpenAIStreamParser streamParser,
        IProviderCredentialSource credentials,
        HttpClient httpClient,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(translator);
        ArgumentNullException.ThrowIfNull(streamParser);
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(timeProvider);

        Alias = descriptor.Alias;
        _descriptor = descriptor;
        _profile = profile;
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
            AzureOpenAIProviderDefaults.AuthorizationScheme);
        if (authorization is ProviderAuthorizationDenied denied)
        {
            return await FailAsync(denied.Failure).ConfigureAwait(false);
        }

        var granted = (ProviderAuthorizationGranted) authorization;
        var useStreaming = _profile.PreferStreaming && _descriptor.Capabilities.SupportsStreaming;

        JsonObject payload;
        try
        {
            payload = _translator.Translate(request, _profile, useStreaming);
        }
        catch (NotSupportedException exception)
        {
            return await FailWithKindAsync(
                ProviderFailureKind.InvalidRequest,
                "The request could not be translated for the Azure OpenAI GA v1 wire format.",
                exception).ConfigureAwait(false);
        }

        if (_descriptor.DeploymentId is { } deploymentId)
        {
            payload["model"] = deploymentId.Value;
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

            var parseContext = new OpenAIResponseParseContext(
                requestId,
                _descriptor.ProviderId,
                _descriptor.ApiFamily,
                _descriptor.ModelId,
                _descriptor.DeploymentId,
                ProviderRequestIdReader.TryRead(response.Headers, _requestIdHeaderName));

            try
            {
                var body = await response.Content.ReadAsStreamAsync(linkedSource.Token).ConfigureAwait(false);
                await using (body.ConfigureAwait(false))
                {
                    return useStreaming
                        ? await _streamParser
                            .ParseStreamingAsync(body, parseContext, sequencing, linkedSource.Token)
                            .ConfigureAwait(false)
                        : await _streamParser
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

    private HttpRequestMessage CreateHttpRequest(JsonObject payload, ProviderAuthorizationGranted authorization)
    {
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, _profile.ChatCompletionsUri)
        {
            Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json"),
        };

        authorization.Apply(httpRequest.Headers);

        foreach (var header in _profile.DefaultRequestHeaders)
        {
            _ = httpRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return httpRequest;
    }

    private async Task<ProviderFailure> BuildHttpFailureAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        string? providerMessage = null;
        string? providerCode = null;
        Exception? diagnosticCause = null;

        try
        {
            var body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using (body.ConfigureAwait(false))
            {
                var envelope = await JsonSerializer
                    .DeserializeAsync<AzureOpenAIErrorEnvelopeDto>(body, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                providerMessage = envelope?.Error?.Message;
                providerCode = envelope?.Error?.Code ?? envelope?.Error?.Type;
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The error body was malformed, truncated, or the connection failed while reading it. The HTTP status
            // is still authoritative evidence, so fall back to a status-only failure and keep the cause for diagnostics.
            diagnosticCause = exception;
        }

        return new ProviderFailure(
            HttpStatusFailureKindMapper.Map(response.StatusCode),
            _descriptor.ProviderId,
            ProviderRequestIdReader.TryRead(response.Headers, _requestIdHeaderName),
            (int) response.StatusCode,
            providerCode,
            RetryAfterResolver.Resolve(response.Headers, _timeProvider),
            $"The provider returned HTTP status {(int) response.StatusCode}.",
            diagnosticCause,
            ProviderErrorMessageEvidence.Create(providerMessage));
    }

    /// <summary>Builds an interrupted error-body failure while preserving response evidence already received.</summary>
    /// <param name="response">The response whose headers were received before interruption.</param>
    /// <param name="kind">The normalized interruption classification.</param>
    /// <param name="safeMessage">The bounded message safe for ordinary application handling.</param>
    /// <param name="diagnosticCause">The classified exception that interrupted response-body processing.</param>
    /// <returns>A failure retaining the raw HTTP status, Retry-After guidance, and Azure request identity.</returns>
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
