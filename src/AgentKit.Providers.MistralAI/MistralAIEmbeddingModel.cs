// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.MistralAI;

using System.Net.Http;

using AgentKit.Providers.Http;
using AgentKit.Providers.MistralAI.Wire;

/// <summary>
/// The Mistral AI embedding <see cref="IEmbeddingModel"/>, performing
/// translation, credential resolution, transport, and response parsing for
/// the Mistral AI embeddings API directly.
/// </summary>
public sealed class MistralAIEmbeddingModel: IEmbeddingModel
{
    private readonly EmbeddingModelDescriptor _descriptor;
    private readonly MistralAIProviderOptions _options;
    private readonly IMistralAIEmbeddingRequestTranslator _translator;
    private readonly IMistralAIEmbeddingResponseParser _responseParser;
    private readonly IProviderCredentialSource _credentials;
    private readonly HttpClient _httpClient;
    private readonly TimeProvider _timeProvider;

    /// <summary>Initializes a new instance of the <see cref="MistralAIEmbeddingModel"/> class.</summary>
    /// <param name="descriptor">The descriptor of the model this instance serves.</param>
    /// <param name="options">The validated Mistral AI provider options.</param>
    /// <param name="translator">Translates provider-neutral requests into Mistral AI embeddings request bodies.</param>
    /// <param name="responseParser">Parses Mistral AI embeddings responses into normalized results.</param>
    /// <param name="credentials">Resolves the current credential for <paramref name="descriptor"/>'s provider.</param>
    /// <param name="httpClient">The HTTP client used to send requests.</param>
    /// <param name="timeProvider">The clock used for deadline and credential-expiry evaluation.</param>
    /// <exception cref="ArgumentNullException">Any parameter is null.</exception>
    public MistralAIEmbeddingModel(
        EmbeddingModelDescriptor descriptor,
        MistralAIProviderOptions options,
        IMistralAIEmbeddingRequestTranslator translator,
        IMistralAIEmbeddingResponseParser responseParser,
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
            MistralAIProviderDefaults.AuthorizationScheme);
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
                "The request could not be translated for the Mistral AI embeddings wire format.",
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
                return new EmbeddingAttemptFailed(await BuildHttpFailureAsync(response, linkedSource.Token).ConfigureAwait(false));
            }

            var parseContext = new MistralAIEmbeddingResponseParseContext(
                request.Context.RequestId,
                _descriptor.ProviderId,
                _descriptor.ApiFamily,
                _descriptor.ModelId,
                request.Context.Request.Encoding);

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
        }
    }

    private HttpRequestMessage CreateHttpRequest(JsonObject payload, ProviderAuthorizationGranted authorization)
    {
        var uri = MistralAIProviderDefaults.BuildEmbeddingsUri(_options);
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
