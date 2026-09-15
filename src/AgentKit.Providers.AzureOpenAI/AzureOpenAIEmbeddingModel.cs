// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AzureOpenAI;

using System.Diagnostics;
using System.Net.Http;

using AgentKit.Providers.AzureOpenAI.Wire;
using AgentKit.Providers.Http;
using AgentKit.Providers.OpenAICompatible;

/// <summary>
/// The Azure OpenAI embedding <see cref="IEmbeddingModel"/>, performing
/// Azure-specific credential resolution, endpoint construction, and
/// transport, while delegating request-body translation and response
/// parsing to the shared OpenAI-compatible wire-shape collaborators from
/// <c>AgentKit.Providers.OpenAICompatible</c>.
/// </summary>
/// <remarks>
/// <para>
/// This class does not derive from <see cref="OpenAICompatibleEmbeddingModelBase"/>
/// because that base class hardcodes <c>Authorization: Bearer</c>
/// authentication, which cannot represent Azure's <c>api-key</c> header
/// credential shape. Instead, it implements <see cref="IEmbeddingModel"/>
/// directly and reuses the base class's injectable, provider-neutral
/// <see cref="IOpenAIEmbeddingRequestTranslator"/> and
/// <see cref="IOpenAIEmbeddingResponseParser"/> collaborators, which those
/// interfaces were designed to support: the request/response wire shape is
/// genuinely OpenAI-compatible even though authentication and endpoint
/// construction are not.
/// </para>
/// <para>
/// Azure's GA v1 dialect addresses a model by Azure deployment name (via
/// <see cref="EmbeddingModelDescriptor.DeploymentId"/>) rather than the
/// underlying model identity (<see cref="EmbeddingModelDescriptor.ModelId"/>)
/// alone, so after translation this class overwrites the body's
/// <c>model</c> field with the deployment name whenever one is configured.
/// </para>
/// </remarks>
public sealed class AzureOpenAIEmbeddingModel: IEmbeddingModel
{
    /// <summary>The response header Azure OpenAI uses to return its request identifier.</summary>
    private const string _requestIdHeaderName = "x-request-id";

    private readonly EmbeddingModelDescriptor _descriptor;
    private readonly OpenAICompatibilityProfile _profile;
    private readonly IOpenAIEmbeddingRequestTranslator _translator;
    private readonly IOpenAIEmbeddingResponseParser _responseParser;
    private readonly IProviderCredentialSource _credentials;
    private readonly HttpClient _httpClient;
    private readonly TimeProvider _timeProvider;

    /// <summary>Initializes a new instance of the <see cref="AzureOpenAIEmbeddingModel"/> class.</summary>
    /// <param name="descriptor">
    /// The descriptor of the model this instance serves; its
    /// <see cref="EmbeddingModelDescriptor.DeploymentId"/> supplies the
    /// Azure deployment name sent as the wire <c>model</c> field.
    /// </param>
    /// <param name="profile">The tested wire-behavior configuration for the target resource endpoint.</param>
    /// <param name="translator">Translates provider-neutral requests into OpenAI-compatible request bodies.</param>
    /// <param name="responseParser">Parses OpenAI-compatible embeddings responses into normalized results.</param>
    /// <param name="credentials">Resolves the current credential for <paramref name="descriptor"/>'s provider.</param>
    /// <param name="httpClient">The HTTP client used to send requests.</param>
    /// <param name="timeProvider">The clock used for deadline and credential-expiry evaluation.</param>
    /// <exception cref="ArgumentNullException">Any parameter is null.</exception>
    public AzureOpenAIEmbeddingModel(
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
            AzureOpenAIProviderDefaults.AuthorizationScheme);
        if (authorization is ProviderAuthorizationDenied denied)
        {
            return new EmbeddingAttemptFailed(denied.Failure);
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
                "The request could not be translated for the Azure OpenAI GA v1 embeddings wire format.",
                exception);
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
                try
                {
                    return new EmbeddingAttemptFailed(await BuildHttpFailureAsync(response, linkedSource.Token).ConfigureAwait(false));
                }
                catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
                {
                    return new EmbeddingAttemptCancelled(BuildInterruptedHttpFailure(response, ProviderFailureKind.Cancellation, "The attempt was cancelled while receiving the provider error response.", exception));
                }
                catch (OperationCanceledException exception) when (deadlineSource.IsCancellationRequested)
                {
                    return new EmbeddingAttemptFailed(BuildInterruptedHttpFailure(response, ProviderFailureKind.Timeout, "The provider error response was not received before the request deadline.", exception));
                }
                catch (OperationCanceledException exception)
                {
                    return new EmbeddingAttemptFailed(BuildInterruptedHttpFailure(response, ProviderFailureKind.Timeout, "The transport timed out while the provider error response was being received.", exception));
                }
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
        string? safeMessage = null;
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
                safeMessage = envelope?.Error?.Message;
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
            safeMessage ?? $"The provider returned HTTP status {(int) response.StatusCode}.",
            diagnosticCause,
            ExtensionData.Empty);
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
