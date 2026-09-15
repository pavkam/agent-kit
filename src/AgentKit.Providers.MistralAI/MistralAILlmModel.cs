// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.MistralAI;

using System.Net.Http;

using AgentKit.Providers.Http;
using AgentKit.Providers.MistralAI.Wire;

/// <summary>
/// The Mistral AI conversational <see cref="ILlmModel"/>, performing
/// translation, credential resolution, transport, and response parsing for
/// the Mistral AI Chat Completions API directly.
/// </summary>
/// <remarks>
/// Like Anthropic and Gemini, Mistral AI has exactly one AgentKit
/// integration today, so this class implements <see cref="ILlmModel"/>
/// directly rather than deriving from a shared protocol-family base class.
/// Although Mistral's Chat Completions response shape (<c>choices[]</c>,
/// <c>delta</c>) resembles the OpenAI-compatible family, its
/// <c>tool_choice</c> vocabulary, <c>random_seed</c> field name,
/// content-chunk union, and reasoning shape differ enough that it uses its
/// own <see cref="IMistralAIRequestTranslator"/> and
/// <see cref="IMistralAIResponseParser"/> rather than the shared
/// <c>AgentKit.Providers.OpenAICompatible</c> base.
/// </remarks>
public sealed class MistralAILlmModel: ILlmModel
{
    private readonly ModelDescriptor _descriptor;
    private readonly MistralAIProviderOptions _options;
    private readonly IMistralAIRequestTranslator _translator;
    private readonly IMistralAIResponseParser _responseParser;
    private readonly IProviderCredentialSource _credentials;
    private readonly HttpClient _httpClient;
    private readonly TimeProvider _timeProvider;

    /// <summary>Initializes a new instance of the <see cref="MistralAILlmModel"/> class.</summary>
    /// <param name="descriptor">The descriptor of the model this instance serves.</param>
    /// <param name="options">The validated Mistral AI provider options.</param>
    /// <param name="translator">Translates provider-neutral requests into Mistral AI Chat Completions request bodies.</param>
    /// <param name="responseParser">Parses Mistral AI Chat Completions responses into normalized events.</param>
    /// <param name="credentials">Resolves the current credential for <paramref name="descriptor"/>'s provider.</param>
    /// <param name="httpClient">The HTTP client used to send requests.</param>
    /// <param name="timeProvider">The clock used for deadline and credential-expiry evaluation.</param>
    /// <exception cref="ArgumentNullException">Any parameter is null.</exception>
    public MistralAILlmModel(
        ModelDescriptor descriptor,
        MistralAIProviderOptions options,
        IMistralAIRequestTranslator translator,
        IMistralAIResponseParser responseParser,
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

        var authorization = ProviderAuthorizationHeaderFactory.Create(
            credential,
            _descriptor.ProviderId,
            _timeProvider,
            MistralAIProviderDefaults.AuthorizationScheme);
        if (authorization is ProviderAuthorizationDenied denied)
        {
            return await FailAsync(denied.Failure).ConfigureAwait(false);
        }

        var granted = (ProviderAuthorizationGranted) authorization;
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
                "The request could not be translated for the Mistral AI Chat Completions wire format.",
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

            var parseContext = new MistralAIResponseParseContext(
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

    private HttpRequestMessage CreateHttpRequest(JsonObject payload, ProviderAuthorizationGranted authorization)
    {
        var uri = MistralAIProviderDefaults.BuildChatCompletionsUri(_options);
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json"),
        };

        authorization.Apply(httpRequest.Headers);

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
                    .DeserializeAsync<MistralAIErrorEnvelopeDto>(body, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                safeMessage = ExtractDetailMessage(envelope?.Detail);
            }
        }
        catch (JsonException)
        {
            // The error body was not valid JSON; fall back to a generic message below.
        }

        return new ProviderFailure(
            HttpStatusFailureKindMapper.Map(response.StatusCode),
            _descriptor.ProviderId,
            requestId: null,
            (int) response.StatusCode,
            providerCode: null,
            RetryAfterResolver.Resolve(response.Headers, _timeProvider),
            safeMessage ?? $"The provider returned HTTP status {(int) response.StatusCode}.",
            diagnosticCause: null,
            ExtensionData.Empty);
    }

    private static string? ExtractDetailMessage(JsonElement? detail)
    {
        if (detail is not { } element)
        {
            return null;
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            return element.GetString();
        }

        if (element.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var messages = new List<string>();
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("msg", out var msg) && msg.ValueKind == JsonValueKind.String)
            {
                messages.Add(msg.GetString()!);
            }
        }

        return messages.Count > 0 ? string.Join(" ", messages) : null;
    }
}
