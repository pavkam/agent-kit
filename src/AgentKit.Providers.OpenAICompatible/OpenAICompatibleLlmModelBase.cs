// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible;

using System.Diagnostics;
using System.Net.Http;

using AgentKit.Providers.Http;
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
/// Two narrow extension points let a branded dialect diverge without
/// re-implementing the pipeline: <see cref="AuthorizationScheme"/> selects
/// the header shape a credential is sent in (defaulting to
/// <c>Authorization: Bearer</c>), and <see cref="AdjustRequestPayload"/>
/// lets a derived class amend the translated JSON body immediately before
/// it is serialized (for example, to address a model by deployment name).
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
    /// <summary>The response header OpenAI-compatible endpoints use to return their request identifier.</summary>
    private const string _requestIdHeaderName = "x-request-id";

    /// <summary>
    /// The largest delay <see cref="CancellationTokenSource(TimeSpan, TimeProvider)"/> accepts
    /// (<see cref="uint.MaxValue"/> - 1 milliseconds, ~49.7 days). A caller expressing "no practical
    /// deadline" (a far-future <see cref="LlmModelRequest.Deadline"/>) must not turn every attempt into
    /// an unhandled <see cref="ArgumentOutOfRangeException"/> after credential resolution and
    /// translation have already run.
    /// </summary>
    private static readonly TimeSpan _maximumDeadlineDelay = TimeSpan.FromMilliseconds(uint.MaxValue - 1);

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
        Descriptor = descriptor;
        _profile = profile;
        _translator = translator;
        _streamParser = streamParser;
        _credentials = credentials;
        _httpClient = httpClient;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc/>
    public ModelAlias Alias { get; }

    /// <summary>
    /// Gets the immutable descriptor of the model this adapter serves, as
    /// supplied to the constructor. It is the same instance every request is
    /// preflight-checked against, so a derived class may read its identity
    /// (for example, <see cref="ModelDescriptor.DeploymentId"/>) when
    /// adjusting a payload without consulting the request again.
    /// </summary>
    /// <value>The non-null descriptor whose <see cref="ModelDescriptor.Alias"/> equals <see cref="Alias"/>.</value>
    protected ModelDescriptor Descriptor { get; }

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
    /// <see cref="IOpenAIRequestTranslator"/> for this attempt. It is owned by
    /// the current attempt only; the override may add, replace, or remove
    /// members but must not retain a reference beyond the call.
    /// </param>
    /// <param name="request">The request being sent, already preflight-validated against <see cref="Descriptor"/>.</param>
    /// <remarks>
    /// This hook runs after successful translation and credential
    /// resolution and before the HTTP request is created, so an override
    /// cannot influence capability preflight, authorization, or deadline
    /// evaluation. It is invoked on the request path of every attempt and
    /// must be thread-safe. An exception thrown by an override escapes
    /// <see cref="ExecuteAsync"/> unwrapped; overrides should therefore
    /// perform only deterministic, non-throwing payload edits.
    /// </remarks>
    protected virtual void AdjustRequestPayload(JsonObject payload, LlmModelRequest request)
    {
    }

    /// <inheritdoc/>
    public async Task<ModelAttemptResult> ExecuteAsync(
        LlmModelRequest request,
        IModelResponseObserver observer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(observer);

        var requestId = request.Context.ModelRequestId;

        // This adapter and its wire parser both emit events; the sequencing wrapper guarantees the observer
        // sees exactly one ModelResponseStarted, contiguous sequences, and nothing after a terminal event. It
        // also retains the parts already completed and the latest usage, so an attempt interrupted outside the
        // parser (transport fault, deadline, caller cancellation) still settles with truthful partial output.
        var sequencing = new SequencingModelResponseObserver(observer);

        async Task<ModelAttemptResult> FailAsync(ProviderFailure failure)
        {
            await EnsureStartedAsync(cancellationToken).ConfigureAwait(false);
            await sequencing.OnEventAsync(
                    new ModelResponseFailed(
                        requestId,
                        sequencing.NextSequence,
                        failure,
                        sequencing.CompletedParts,
                        sequencing.Usage),
                    cancellationToken)
                .ConfigureAwait(false);
            return new ModelAttemptFailed(failure, sequencing.CompletedParts, sequencing.Usage);
        }

        Task<ModelAttemptResult> FailWithKindAsync(ProviderFailureKind kind, string safeMessage, Exception? cause = null) =>
            FailAsync(new ProviderFailure(
                kind,
                Descriptor.ProviderId,
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
                Descriptor.ProviderId,
                requestId: null,
                statusCode: null,
                providerCode: null,
                retryAfter: null,
                "The attempt was cancelled.",
                diagnosticCause: null,
                ExtensionData.Empty);

            await EnsureStartedAsync(CancellationToken.None).ConfigureAwait(false);
            await sequencing.OnEventAsync(
                    new ModelResponseCancelled(
                        requestId,
                        sequencing.NextSequence,
                        cancellation,
                        sequencing.CompletedParts,
                        sequencing.Usage),
                    CancellationToken.None)
                .ConfigureAwait(false);
            return new ModelAttemptCancelled(cancellation, sequencing.CompletedParts, sequencing.Usage);
        }

        ValueTask EnsureStartedAsync(CancellationToken deliveryToken) =>
            sequencing.HasStarted
                ? ValueTask.CompletedTask
                : sequencing.OnEventAsync(new ModelResponseStarted(requestId, sequencing.NextSequence), deliveryToken);

        if (ModelRequestPreflight.Validate(request, Descriptor) is { } preflightFailure)
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
            credential = await _credentials.GetCredentialAsync(Descriptor.ProviderId, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return await CancelAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException exception)
        {
            // The credential source's own internal timeout (e.g. a token provider's HTTP call), not the
            // caller's cancellation: the same distinction the transport path below already makes.
            return await FailWithKindAsync(
                ProviderFailureKind.Timeout,
                "The credential source did not resolve a credential before its own deadline.",
                exception).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            // IProviderCredentialSource.GetCredentialAsync is user-supplied and routinely fails with
            // provider-specific exceptions (e.g. an Entra/Azure.Identity token provider's
            // AuthenticationFailedException). Every other auth failure in this class reports
            // Authentication; an uncaught exception here would break the "terminal outcome equals last
            // event" contract instead.
            return await FailWithKindAsync(
                ProviderFailureKind.Authentication,
                "The request credential could not be resolved.",
                exception).ConfigureAwait(false);
        }

        var authorization = ProviderAuthorizationHeaderFactory.Create(
            credential,
            Descriptor.ProviderId,
            _timeProvider,
            AuthorizationScheme);
        if (authorization is ProviderAuthorizationDenied denied)
        {
            return await FailAsync(denied.Failure).ConfigureAwait(false);
        }

        var granted = (ProviderAuthorizationGranted) authorization;

        var useStreaming = _profile.PreferStreaming && Descriptor.Capabilities.SupportsStreaming;

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

        AdjustRequestPayload(payload, request);

        using var httpRequest = CreateHttpRequest(payload, granted);
        using var deadlineSource = new CancellationTokenSource(
            remaining > _maximumDeadlineDelay ? _maximumDeadlineDelay : remaining, _timeProvider);
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

            var parseContext = new ProviderResponseParseContext(
                requestId,
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
