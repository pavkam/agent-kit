// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible;

using System.Diagnostics;
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
/// Two narrow extension points let a branded dialect diverge without
/// re-implementing the pipeline: <see cref="AuthorizationScheme"/> selects
/// the header shape a credential is sent in (defaulting to
/// <c>Authorization: Bearer</c>), and <see cref="AdjustRequestPayload"/>
/// lets a derived class amend the translated JSON body immediately before
/// it is serialized (for example, to address a model by deployment name).
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
        Descriptor = descriptor;
        _profile = profile;
        _translator = translator;
        _responseParser = responseParser;
        _credentials = credentials;
        _httpClient = httpClient;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc/>
    public EmbeddingModelAlias Alias { get; }

    /// <summary>
    /// Gets the immutable descriptor of the model this adapter serves, as
    /// supplied to the constructor. It is the same instance every request is
    /// preflight-checked against, so a derived class may read its identity
    /// (for example, <see cref="EmbeddingModelDescriptor.DeploymentId"/>)
    /// when adjusting a payload without consulting the request again.
    /// </summary>
    /// <value>The non-null descriptor whose <see cref="EmbeddingModelDescriptor.Alias"/> equals <see cref="Alias"/>.</value>
    protected EmbeddingModelDescriptor Descriptor { get; }

    /// <summary>
    /// Gets the header authentication scheme a resolved
    /// <see cref="ProviderCredential"/> is applied with. The default sends an
    /// API key or OAuth token as <c>Authorization: Bearer &lt;secret&gt;</c>.
    /// </summary>
    /// <value>
    /// A non-null scheme. A derived class overrides this to select a
    /// different verified header shape, such as
    /// <see cref="ProviderAuthorizationScheme.ForApiKeyHeader"/> for
    /// dialects that carry API keys in a dedicated header while still
    /// sending OAuth tokens as bearer tokens.
    /// </value>
    /// <remarks>
    /// The value is read once per attempt, after the credential has been
    /// resolved and before any HTTP request is created. Overrides must be
    /// pure and thread-safe: this adapter is shared across concurrent
    /// attempts.
    /// </remarks>
    protected virtual ProviderAuthorizationScheme AuthorizationScheme => ProviderAuthorizationScheme.BearerToken;

    /// <summary>
    /// Amends the translated request body immediately before it is
    /// serialized and sent. The default implementation makes no change.
    /// </summary>
    /// <param name="payload">
    /// The mutable JSON object produced by the injected
    /// <see cref="IOpenAIEmbeddingRequestTranslator"/> for this attempt. It
    /// is owned by the current attempt only; the override may add, replace,
    /// or remove members but must not retain a reference beyond the call.
    /// </param>
    /// <param name="request">The request being sent, already preflight-validated against <see cref="Descriptor"/>.</param>
    /// <remarks>
    /// This hook runs after successful translation and credential
    /// resolution and before the HTTP request is created, so an override
    /// cannot influence capability preflight, authorization, or deadline
    /// evaluation. It is invoked on the request path of every attempt and
    /// must be thread-safe. An exception thrown by an override escapes
    /// <see cref="GenerateAsync"/> unwrapped; overrides should therefore
    /// perform only deterministic, non-throwing payload edits.
    /// </remarks>
    protected virtual void AdjustRequestPayload(JsonObject payload, EmbeddingModelRequest request)
    {
    }

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
                Descriptor.ProviderId,
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

        if (ModelRequestPreflight.Validate(request, Descriptor) is { } preflightFailure)
        {
            return Fail(preflightFailure);
        }

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
            credential = await _credentials.GetCredentialAsync(Descriptor.ProviderId, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Cancel(Descriptor.ProviderId);
        }

        var authorization = ProviderAuthorizationHeaderFactory.Create(
            credential,
            Descriptor.ProviderId,
            _timeProvider,
            AuthorizationScheme);
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

        AdjustRequestPayload(payload, request);

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
            return Cancel(Descriptor.ProviderId);
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
                    return Fail(await BuildHttpFailureAsync(response, linkedSource.Token).ConfigureAwait(false));
                }
                catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
                {
                    return new EmbeddingAttemptCancelled(BuildInterruptedHttpFailure(response, ProviderFailureKind.Cancellation, "The attempt was cancelled while receiving the provider error response.", exception));
                }
                catch (OperationCanceledException exception) when (deadlineSource.IsCancellationRequested)
                {
                    return Fail(BuildInterruptedHttpFailure(response, ProviderFailureKind.Timeout, "The provider error response was not received before the request deadline.", exception));
                }
                catch (OperationCanceledException exception)
                {
                    return Fail(BuildInterruptedHttpFailure(response, ProviderFailureKind.Timeout, "The transport timed out while the provider error response was being received.", exception));
                }
            }

            var parseContext = new OpenAIEmbeddingResponseParseContext(
                request.Context.RequestId,
                Descriptor.ProviderId,
                Descriptor.ApiFamily,
                Descriptor.ModelId,
                Descriptor.DeploymentId,
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
                return Cancel(Descriptor.ProviderId);
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
        string? providerMessage = null;
        string? providerCode = null;
        Exception? diagnosticCause = null;

        try
        {
            var body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using (body.ConfigureAwait(false))
            {
                var error = await JsonSerializer
                    .DeserializeAsync<OpenAIErrorResponse>(body, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                providerMessage = error?.Error?.Message;
                providerCode = error?.Error?.Code ?? error?.Error?.Type;
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The error body was malformed, truncated, or the connection failed while reading it. The HTTP status
            // is still authoritative evidence, so fall back to a status-only failure and keep the cause for diagnostics.
            diagnosticCause = exception;
        }

        // The provider's message is untrusted content: it is retained only as bounded diagnostic evidence under a
        // dedicated extension key and never promoted into the safe message.
        return new ProviderFailure(
            HttpStatusFailureKindMapper.Map(response.StatusCode),
            Descriptor.ProviderId,
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
    /// <returns>A failure retaining the raw HTTP status, Retry-After guidance, and provider request identity.</returns>
    private ProviderFailure BuildInterruptedHttpFailure(HttpResponseMessage response, ProviderFailureKind kind, string safeMessage, Exception? diagnosticCause)
    {
        Debug.Assert(response is not null, "The error response must have been received before its body read can be interrupted.");
        Debug.Assert(Enum.IsDefined(kind), "The interruption kind must be a defined provider failure kind.");
        Debug.Assert(!string.IsNullOrWhiteSpace(safeMessage), "The interrupted failure must have a bounded safe message.");

        return new ProviderFailure(
            kind,
            Descriptor.ProviderId,
            ProviderRequestIdReader.TryRead(response.Headers, _requestIdHeaderName),
            (int) response.StatusCode,
            providerCode: null,
            RetryAfterResolver.Resolve(response.Headers, _timeProvider),
            safeMessage,
            diagnosticCause,
            ExtensionData.Empty);
    }
}
