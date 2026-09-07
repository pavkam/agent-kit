// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AwsBedrock;

using System.Net.Http;
using System.Net.Http.Headers;

using AgentKit.Providers.AwsBedrock.Wire;

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

            var parseContext = new AwsBedrockResponseParseContext(
                requestId,
                _descriptor.ProviderId,
                _descriptor.ApiFamily,
                _descriptor.ModelId,
                _descriptor.DeploymentId,
                TryReadProviderRequestId(response));

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
        string? safeMessage = null;

        try
        {
            var body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using (body.ConfigureAwait(false))
            {
                var envelope = await JsonSerializer
                    .DeserializeAsync<AwsBedrockErrorEnvelopeDto>(body, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                safeMessage = envelope?.Message;
            }
        }
        catch (JsonException)
        {
            // The error body was not valid JSON; fall back to a generic message below.
        }

        var errorType = TryReadErrorType(response);
        var kind = errorType is not null
            ? AwsBedrockErrorMapping.MapExceptionName(errorType)
            : AwsBedrockErrorMapping.MapStatusCode(response.StatusCode);

        return new ProviderFailure(
            kind,
            _descriptor.ProviderId,
            TryReadProviderRequestId(response),
            (int) response.StatusCode,
            errorType,
            response.Headers.RetryAfter?.Delta,
            safeMessage ?? $"The provider returned HTTP status {(int) response.StatusCode}.",
            diagnosticCause: null,
            ExtensionData.Empty);
    }

    private static string? TryReadErrorType(HttpResponseMessage response) =>
        response.Headers.TryGetValues("x-amzn-errortype", out var values) && values.FirstOrDefault() is { Length: > 0 } value
            ? value
            : null;

    private static ProviderRequestId? TryReadProviderRequestId(HttpResponseMessage response) =>
        response.Headers.TryGetValues("x-amzn-RequestId", out var values) && values.FirstOrDefault() is { Length: > 0 } value
            ? new ProviderRequestId(value)
            : null;
}
