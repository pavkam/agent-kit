// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible;

using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;

using AgentKit.Providers.OpenAICompatible.Wire;

/// <summary>
/// A reusable <see cref="ILlmModel"/> implementation for OpenAI-compatible
/// Chat Completions endpoints, performing translation, credential
/// resolution, transport, and stream parsing through injected collaborators.
/// </summary>
/// <remarks>
/// <para>
/// A concrete provider package (such as AgentKit.Providers.OpenAI) derives
/// from this class and supplies its own <see cref="ModelDescriptor"/>,
/// <see cref="OpenAICompatibilityProfile"/>, and
/// <see cref="IProviderCredentialSource"/>. This base class owns the shared
/// request pipeline: capability pre-check, translation, credential-to-header
/// resolution, deadline enforcement, transport, response parsing, and
/// normalized failure mapping.
/// </para>
/// <para>
/// This implementation performs its own HTTP transport through an injected
/// <see cref="HttpClient"/> rather than the broader AgentKit network and
/// security-authority abstractions described by the wider provider
/// architecture, because those packages do not yet exist in this
/// repository. A future revision can route through them without changing
/// this class's public contract.
/// </para>
/// </remarks>
public abstract class OpenAICompatibleLlmModelBase: ILlmModel
{
    private readonly ModelDescriptor _descriptor;
    private readonly OpenAICompatibilityProfile _profile;
    private readonly IOpenAIRequestTranslator _translator;
    private readonly IOpenAIStreamParser _streamParser;
    private readonly IProviderCredentialSource _credentials;
    private readonly HttpClient _httpClient;
    private readonly TimeProvider _timeProvider;

    /// <summary>Initializes a new instance of the <see cref="OpenAICompatibleLlmModelBase"/> class.</summary>
    /// <param name="descriptor">
    /// The descriptor of the model this instance serves. Its
    /// <see cref="ModelDescriptor.Alias"/> becomes <see cref="Alias"/>.
    /// </param>
    /// <param name="profile">The tested wire-behavior configuration for the target endpoint.</param>
    /// <param name="translator">Translates provider-neutral requests into OpenAI-compatible request bodies.</param>
    /// <param name="streamParser">Parses OpenAI-compatible responses into normalized events.</param>
    /// <param name="credentials">Resolves the current credential for <paramref name="descriptor"/>'s provider.</param>
    /// <param name="httpClient">The HTTP client used to send requests.</param>
    /// <param name="timeProvider">The clock used for deadline and credential-expiry evaluation.</param>
    /// <exception cref="ArgumentNullException">Any parameter is null.</exception>
    protected OpenAICompatibleLlmModelBase(
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

        var requestId = request.Context.ModelRequestId;
        var trackingObserver = new TrackingModelResponseObserver(observer);

        async Task<ModelAttemptResult> FailAsync(ProviderFailure failure)
        {
            await EnsureStartedAsync(cancellationToken).ConfigureAwait(false);
            await trackingObserver.OnEventAsync(
                    new ModelResponseFailed(
                        requestId,
                        trackingObserver.NextSequence,
                        failure,
                        trackingObserver.CompletedParts,
                        trackingObserver.Usage),
                    cancellationToken)
                .ConfigureAwait(false);
            return new ModelAttemptFailed(failure, trackingObserver.CompletedParts, trackingObserver.Usage);
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

            await EnsureStartedAsync(CancellationToken.None).ConfigureAwait(false);
            await trackingObserver.OnEventAsync(
                    new ModelResponseCancelled(
                        requestId,
                        trackingObserver.NextSequence,
                        cancellation,
                        trackingObserver.CompletedParts,
                        trackingObserver.Usage),
                    CancellationToken.None)
                .ConfigureAwait(false);
            return new ModelAttemptCancelled(cancellation, trackingObserver.CompletedParts, trackingObserver.Usage);
        }

        ValueTask EnsureStartedAsync(CancellationToken deliveryToken) =>
            trackingObserver.NextSequence == 0
                ? trackingObserver.OnEventAsync(new ModelResponseStarted(requestId, 0), deliveryToken)
                : ValueTask.CompletedTask;

        if (request.Context.Model != _descriptor)
        {
            return await FailWithKindAsync(
                ProviderFailureKind.InvalidRequest,
                "The request model descriptor does not match the configured adapter descriptor.").ConfigureAwait(false);
        }

        if (request.Context.Tools.Length > 0 && !_descriptor.Capabilities.SupportsToolCalls)
        {
            return await FailWithKindAsync(
                ProviderFailureKind.InvalidRequest,
                "The selected model does not support tool calls.").ConfigureAwait(false);
        }

        if (request.Context.Tools.Length > 0 &&
            request.Context.Settings.ParallelToolCalls is true &&
            !_descriptor.Capabilities.SupportsParallelToolCalls)
        {
            return await FailWithKindAsync(
                ProviderFailureKind.InvalidRequest,
                "The selected model does not support parallel tool calls.").ConfigureAwait(false);
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

        var authorization = OpenAIAuthorizationHeaderFactory.Create(credential, _descriptor.ProviderId, _timeProvider);
        if (authorization is OpenAIAuthorizationDenied denied)
        {
            return await FailAsync(denied.Failure).ConfigureAwait(false);
        }

        var granted = (OpenAIAuthorizationGranted) authorization;

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
                "The request could not be translated for the OpenAI-compatible wire format.",
                exception).ConfigureAwait(false);
        }

        using var httpRequest = CreateHttpRequest(payload, granted.Authorization);
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

            var parseContext = new OpenAIResponseParseContext(
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
                        ? await _streamParser
                            .ParseStreamingAsync(body, parseContext, trackingObserver, linkedSource.Token)
                            .ConfigureAwait(false)
                        : await _streamParser
                            .ParseBufferedAsync(body, parseContext, trackingObserver, linkedSource.Token)
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

    private HttpRequestMessage CreateHttpRequest(JsonObject payload, AuthenticationHeaderValue authorization)
    {
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, _profile.ChatCompletionsUri)
        {
            Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json"),
        };

        httpRequest.Headers.Authorization = authorization;

        foreach (var header in _profile.DefaultRequestHeaders)
        {
            _ = httpRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return httpRequest;
    }

    private async Task<ProviderFailure> BuildHttpFailureAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        string? providerCode = null;

        try
        {
            var body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using (body.ConfigureAwait(false))
            {
                var error = await JsonSerializer
                    .DeserializeAsync<OpenAIErrorResponse>(body, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                providerCode = error?.Error?.Code ?? error?.Error?.Type;
            }
        }
        catch (JsonException)
        {
            // The error body was not valid JSON; fall back to a generic message below.
        }

        var kind = MapStatusCode(response.StatusCode);
        var retryAfter = response.Headers.RetryAfter?.Delta
            ?? (response.Headers.RetryAfter?.Date is { } retryAfterDate
                ? retryAfterDate - _timeProvider.GetUtcNow()
                : null);

        return new ProviderFailure(
            kind,
            _descriptor.ProviderId,
            TryReadProviderRequestId(response),
            (int) response.StatusCode,
            providerCode,
            retryAfter,
            $"The provider returned HTTP status {(int) response.StatusCode}.",
            diagnosticCause: null,
            ExtensionData.Empty);
    }

    private static ProviderFailureKind MapStatusCode(HttpStatusCode statusCode)
    {
        var statusCodeValue = (int) statusCode;

        return statusCodeValue switch
        {
            (int) HttpStatusCode.Unauthorized => ProviderFailureKind.Authentication,
            (int) HttpStatusCode.Forbidden => ProviderFailureKind.Authorization,
            (int) HttpStatusCode.TooManyRequests => ProviderFailureKind.Throttling,
            (int) HttpStatusCode.RequestTimeout or (int) HttpStatusCode.GatewayTimeout =>
                ProviderFailureKind.Timeout,
            >= 400 and < 500 => ProviderFailureKind.InvalidRequest,
            >= 500 => ProviderFailureKind.Unavailable,
            _ => ProviderFailureKind.Unknown,
        };
    }

    private static ProviderRequestId? TryReadProviderRequestId(HttpResponseMessage response) =>
        response.Headers.TryGetValues("x-request-id", out var values) && values.FirstOrDefault() is { Length: > 0 } value
            ? new ProviderRequestId(value)
            : null;
}
