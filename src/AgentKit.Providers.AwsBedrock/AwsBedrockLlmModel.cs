// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AwsBedrock;

using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;

using AgentKit.Providers.AwsBedrock.Wire;
using AgentKit.Providers.Http;

/// <summary>
/// The Amazon Bedrock Runtime conversational <see cref="ILlmModel"/>,
/// performing translation, AWS Signature Version 4 request signing,
/// transport, and response parsing for the Bedrock Converse/ConverseStream
/// wire format directly.
/// </summary>
/// <remarks>
/// Unlike this repository's other provider integrations, authentication is
/// not an <see cref="IProviderCredentialSource"/>/<see cref="ProviderCredential"/>
/// bearer-token or API-key header; every request is signed in place with
/// AWS Signature Version 4 using an <see cref="IAwsCredentialSource"/>-resolved
/// <see cref="AwsSigV4Credential"/>, and a streaming response is framed as
/// a binary AWS event stream rather than server-sent events.
/// </remarks>
public sealed class AwsBedrockLlmModel: ILlmModel
{
    /// <summary>The response header Bedrock Runtime uses to return its request identifier.</summary>
    private const string _requestIdHeaderName = "x-amzn-RequestId";

    private static readonly MediaTypeHeaderValue _jsonContentType = new("application/json");

    private readonly ModelDescriptor _descriptor;
    private readonly AwsBedrockProviderOptions _options;
    private readonly IAwsBedrockRequestTranslator _translator;
    private readonly IAwsBedrockResponseParser _responseParser;
    private readonly IAwsCredentialSource _credentials;
    private readonly HttpClient _httpClient;
    private readonly TimeProvider _timeProvider;

    /// <summary>Initializes a new instance of the <see cref="AwsBedrockLlmModel"/> class.</summary>
    /// <param name="descriptor">
    /// The descriptor of the model this instance serves; when its
    /// <see cref="ModelDescriptor.DeploymentId"/> is set, requests target
    /// that ARN (an inference profile, provisioned throughput, custom
    /// model, or Bedrock Marketplace endpoint) instead of
    /// <see cref="ModelDescriptor.ModelId"/> directly.
    /// </param>
    /// <param name="options">The validated Bedrock provider options.</param>
    /// <param name="translator">Translates provider-neutral requests into Converse request bodies.</param>
    /// <param name="responseParser">Parses Converse/ConverseStream responses into normalized events.</param>
    /// <param name="credentials">Resolves the current AWS credential to sign a request with.</param>
    /// <param name="httpClient">The HTTP client used to send requests.</param>
    /// <param name="timeProvider">The clock used for deadline and request-signing timestamps.</param>
    /// <exception cref="ArgumentNullException">Any parameter is null.</exception>
    public AwsBedrockLlmModel(
        ModelDescriptor descriptor,
        AwsBedrockProviderOptions options,
        IAwsBedrockRequestTranslator translator,
        IAwsBedrockResponseParser responseParser,
        IAwsCredentialSource credentials,
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

        AwsSigV4Credential credential;
        try
        {
            credential = await _credentials.GetCredentialAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return await CancelAsync().ConfigureAwait(false);
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
                "The request could not be translated for the Bedrock Converse wire format.",
                exception).ConfigureAwait(false);
        }

        using var httpRequest = CreateHttpRequest(payload, credential, useStreaming);
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

            var parseContext = new AwsBedrockResponseParseContext(
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

    private HttpRequestMessage CreateHttpRequest(JsonObject payload, AwsSigV4Credential credential, bool useStreaming)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(_options.Region);

        var modelId = _descriptor.DeploymentId?.Value ?? _descriptor.ModelId.Value;
        var uri = useStreaming
            ? AwsBedrockProviderDefaults.BuildConverseStreamUri(_options, modelId)
            : AwsBedrockProviderDefaults.BuildConverseUri(_options, modelId);

        var body = Encoding.UTF8.GetBytes(payload.ToJsonString());

        var signingHeaders = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["content-type"] = _jsonContentType.MediaType!,
        };

        var signedHeaders = AwsSigV4Signer.SignRequest(
            HttpMethod.Post.Method,
            uri,
            signingHeaders,
            body,
            credential,
            _options.Region,
            AwsBedrockProviderDefaults.SigningServiceName,
            _timeProvider.GetUtcNow());

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new ByteArrayContent(body),
        };

        httpRequest.Content.Headers.ContentType = _jsonContentType;

        foreach (var (name, value) in signedHeaders)
        {
            _ = httpRequest.Headers.TryAddWithoutValidation(name, value);
        }

        return httpRequest;
    }

    private async Task<ProviderFailure> BuildHttpFailureAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        string? providerMessage = null;
        Exception? diagnosticCause = null;

        try
        {
            var body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using (body.ConfigureAwait(false))
            {
                var envelope = await JsonSerializer
                    .DeserializeAsync<AwsBedrockErrorEnvelopeDto>(body, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                providerMessage = envelope?.Message;
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The error body was malformed, truncated, or the connection failed while reading it. The HTTP status
            // is still authoritative evidence, so fall back to a status-only failure and keep the cause for diagnostics.
            diagnosticCause = exception;
        }

        var errorType = TryReadErrorType(response);
        var kind = errorType is not null
            ? AwsBedrockErrorMapping.MapExceptionName(errorType)
            : AwsBedrockErrorMapping.MapStatusCode(response.StatusCode);

        return new ProviderFailure(
            kind,
            _descriptor.ProviderId,
            ProviderRequestIdReader.TryRead(response.Headers, _requestIdHeaderName),
            (int) response.StatusCode,
            errorType,
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
    /// <returns>A failure retaining the raw HTTP status, Retry-After guidance, Bedrock request identity, and the <c>x-amzn-errortype</c> code when present.</returns>
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
            TryReadErrorType(response),
            RetryAfterResolver.Resolve(response.Headers, _timeProvider),
            safeMessage,
            diagnosticCause,
            ExtensionData.Empty);
    }

    private static string? TryReadErrorType(HttpResponseMessage response) =>
        response.Headers.TryGetValues("x-amzn-errortype", out var values) && values.FirstOrDefault() is { Length: > 0 } value
            ? value
            : null;
}
