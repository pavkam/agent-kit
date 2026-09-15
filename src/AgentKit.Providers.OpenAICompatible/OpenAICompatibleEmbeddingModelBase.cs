// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible;

using System.Net.Http;

using AgentKit.Providers.Http;
using AgentKit.Providers.OpenAICompatible.Wire;

/// <summary>
/// A reusable <see cref="IEmbeddingModel"/> implementation for
/// OpenAI-compatible <c>POST /embeddings</c> endpoints, performing
/// translation, credential resolution, transport, and response parsing
/// through injected collaborators.
/// </summary>
/// <remarks>
/// <para>
/// A concrete provider package (such as AgentKit.Providers.OpenAI) derives
/// from this class and supplies its own <see cref="EmbeddingModelDescriptor"/>,
/// <see cref="OpenAICompatibilityProfile"/>, and
/// <see cref="IProviderCredentialSource"/>. This base class owns the shared
/// request pipeline: translation, credential-to-header resolution, deadline
/// enforcement, transport, response parsing, and normalized failure
/// mapping.
/// </para>
/// <para>
/// Unlike <see cref="OpenAICompatibleLlmModelBase"/>, this operation is not
/// streamed: the OpenAI-compatible embeddings endpoint always returns one
/// buffered JSON response.
/// </para>
/// </remarks>
public abstract class OpenAICompatibleEmbeddingModelBase: IEmbeddingModel
{
    /// <summary>The response header OpenAI-compatible endpoints use to return their request identifier.</summary>
    private const string _requestIdHeaderName = "x-request-id";

    private readonly EmbeddingModelDescriptor _descriptor;
    private readonly OpenAICompatibilityProfile _profile;
    private readonly IOpenAIEmbeddingRequestTranslator _translator;
    private readonly IOpenAIEmbeddingResponseParser _responseParser;
    private readonly IProviderCredentialSource _credentials;
    private readonly HttpClient _httpClient;
    private readonly TimeProvider _timeProvider;

    /// <summary>Initializes a new instance of the <see cref="OpenAICompatibleEmbeddingModelBase"/> class.</summary>
    /// <param name="descriptor">
    /// The descriptor of the model this instance serves. Its
    /// <see cref="EmbeddingModelDescriptor.Alias"/> becomes <see cref="Alias"/>.
    /// </param>
    /// <param name="profile">
    /// The tested wire-behavior configuration for the target endpoint. Its
    /// <see cref="OpenAICompatibilityProfile.EmbeddingsUri"/> must be
    /// configured.
    /// </param>
    /// <param name="translator">Translates provider-neutral requests into OpenAI-compatible request bodies.</param>
    /// <param name="responseParser">Parses OpenAI-compatible embeddings responses into normalized results.</param>
    /// <param name="credentials">Resolves the current credential for <paramref name="descriptor"/>'s provider.</param>
    /// <param name="httpClient">The HTTP client used to send requests.</param>
    /// <param name="timeProvider">The clock used for deadline and credential-expiry evaluation.</param>
    /// <exception cref="ArgumentNullException">Any parameter is null.</exception>
    protected OpenAICompatibleEmbeddingModelBase(
        EmbeddingModelDescriptor descriptor,
        OpenAICompatibilityProfile profile,
        IOpenAIEmbeddingRequestTranslator translator,
        IOpenAIEmbeddingResponseParser responseParser,
        IProviderCredentialSource credentials,
        HttpClient httpClient,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(translator);
        ArgumentNullException.ThrowIfNull(responseParser);
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(timeProvider);

        Alias = descriptor.Alias;
        _descriptor = descriptor;
        _profile = profile;
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

        EmbeddingAttemptResult Fail(ProviderFailure failure) => new EmbeddingAttemptFailed(failure);

        EmbeddingAttemptResult FailWithKind(ProviderFailureKind kind, string safeMessage, Exception? cause = null) =>
            Fail(new ProviderFailure(
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

        if (_profile.EmbeddingsUri is null)
        {
            return FailWithKind(
                ProviderFailureKind.InvalidRequest,
                "The configured compatibility profile does not expose an embeddings endpoint.");
        }

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
            ProviderAuthorizationScheme.BearerToken);
        if (authorization is ProviderAuthorizationDenied denied)
        {
            return Fail(denied.Failure);
        }

        var granted = (ProviderAuthorizationGranted) authorization;

        JsonObject payload;
        try
        {
            payload = _translator.Translate(request, _profile);
        }
        catch (NotSupportedException exception)
        {
            return FailWithKind(
                ProviderFailureKind.InvalidRequest,
                "The request could not be translated for the OpenAI-compatible embeddings wire format.",
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
        catch (OperationCanceledException exception)
        {
            // Neither the caller nor the request deadline cancelled: this is the transport's own timeout
            // (HttpClient.Timeout surfaces as TaskCanceledException). It is a typed timeout, never a caller cancellation.
            return FailWithKind(
                ProviderFailureKind.Timeout,
                "The transport timed out before the provider responded.",
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
                return Fail(await BuildHttpFailureAsync(response, linkedSource.Token).ConfigureAwait(false));
            }

            var parseContext = new OpenAIEmbeddingResponseParseContext(
                request.Context.RequestId,
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
            catch (OperationCanceledException exception)
            {
                return FailWithKind(
                    ProviderFailureKind.Timeout,
                    "The transport timed out while the response body was being received.",
                    exception);
            }
            catch (IOException exception)
            {
                // A connection reset or truncated body mid-stream is a transport failure, not a caller fault.
                return FailWithKind(
                    ProviderFailureKind.Unavailable,
                    "The connection failed while the response body was being received.",
                    exception);
            }
        }
    }

    private HttpRequestMessage CreateHttpRequest(JsonObject payload, ProviderAuthorizationGranted authorization)
    {
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, _profile.EmbeddingsUri)
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

        return new ProviderFailure(
            HttpStatusFailureKindMapper.Map(response.StatusCode),
            _descriptor.ProviderId,
            ProviderRequestIdReader.TryRead(response.Headers, _requestIdHeaderName),
            (int) response.StatusCode,
            providerCode,
            RetryAfterResolver.Resolve(response.Headers, _timeProvider),
            $"The provider returned HTTP status {(int) response.StatusCode}.",
            diagnosticCause: null,
            ExtensionData.Empty);
    }
}
