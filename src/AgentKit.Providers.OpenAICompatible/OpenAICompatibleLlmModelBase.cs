// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible;

using System.Diagnostics;
using System.Net.Http;

using AgentKit.Observability;
using AgentKit.Providers;
using AgentKit.Providers.Egress;
using AgentKit.Providers.Http;
using AgentKit.Providers.OpenAICompatible.Wire;

/// <summary>
/// A reusable <see cref="ILlmModel"/> implementation for OpenAI-compatible
/// Chat Completions endpoints, performing translation, profile selection,
/// transport, and stream parsing through injected collaborators.
/// </summary>
/// <remarks>
/// <para>
/// A concrete provider package (such as AgentKit.Providers.OpenAI) derives
/// from this class and supplies its own <see cref="ModelDescriptor"/>,
/// <see cref="OpenAICompatibilityProfile"/>. This base class owns the shared
/// request pipeline: capability pre-check, translation, endpoint and credential
/// profile selection, deadline enforcement, transport through
/// <see cref="ProviderEgress"/> (which obtains the credential-read grant and
/// lease and applies it), response parsing, and normalized failure mapping.
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
/// Every attempt sends through <see cref="ProviderEgress"/>, which obtains a per-attempt provider-egress grant bound
/// to the descriptor's exact endpoint and credential profile binding and then sends over
/// <see cref="INetworkTransport"/>. The adapter owns no HTTP client: a denied, unauthorizable, or unauditable
/// attempt fails closed with the stable <see cref="ProviderFailure"/> taxonomy before any DNS, connection, or
/// transmission, and an attempt without a <see cref="LlmModelRequest.Operation"/> is refused with
/// <see cref="ProviderFailureKind.Authorization"/>.
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
    /// an unhandled <see cref="ArgumentOutOfRangeException"/> after translation has already run.
    /// </summary>
    private static readonly TimeSpan _maximumDeadlineDelay = TimeSpan.FromMilliseconds(uint.MaxValue - 1);

    private readonly OpenAICompatibilityProfile _profile;
    private readonly IOpenAIRequestTranslator _translator;
    private readonly IOpenAIStreamParser _streamParser;
    private readonly IProviderProfileRuntimeSelector _profileSelector;
    private readonly ProviderEgress _egress;
    private readonly TimeProvider _timeProvider;

    /// <summary>Initializes a new instance of the <see cref="OpenAICompatibleLlmModelBase"/> class.</summary>
    /// <param name="descriptor">
    /// The descriptor of the model this instance serves. Its
    /// <see cref="ModelDescriptor.Alias"/> becomes <see cref="Alias"/>.
    /// </param>
    /// <param name="profile">The tested wire-behavior configuration for the target endpoint.</param>
    /// <param name="translator">Translates provider-neutral requests into OpenAI-compatible request bodies.</param>
    /// <param name="streamParser">Parses OpenAI-compatible responses into normalized events.</param>
    /// <param name="egress">The provider-egress boundary every attempt sends through.</param>
    /// <param name="timeProvider">The clock used for deadline evaluation.</param>
    /// <param name="profileSelector">The engine-wide profile runtime selector that resolves the descriptor's captured endpoint and credential profile binding.</param>
    /// <exception cref="ArgumentNullException">Any parameter is null.</exception>
    protected OpenAICompatibleLlmModelBase(
        ModelDescriptor descriptor,
        OpenAICompatibilityProfile profile,
        IOpenAIRequestTranslator translator,
        IOpenAIStreamParser streamParser,
        ProviderEgress egress,
        TimeProvider timeProvider,
        IProviderProfileRuntimeSelector profileSelector)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(translator);
        ArgumentNullException.ThrowIfNull(streamParser);
        ArgumentNullException.ThrowIfNull(profileSelector);
        ArgumentNullException.ThrowIfNull(egress);
        ArgumentNullException.ThrowIfNull(timeProvider);

        Alias = descriptor.Alias;
        Descriptor = descriptor;
        _profile = profile;
        _translator = translator;
        _streamParser = streamParser;
        _profileSelector = profileSelector;
        _egress = egress;
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
    /// Gets the header authentication scheme the released credential lease
    /// is applied with. The default sends an
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
    /// The value is read once per attempt, after the profile runtime has been
    /// selected and before the request is handed to provider egress. Overrides must be
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
    /// This hook runs after successful translation and before the profile
    /// runtime is selected and the HTTP request is created, so an override
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

        var sendStarted = _timeProvider.GetTimestamp();
        using var sendActivity = ProviderRequestObservability.StartChatSend(Descriptor.ProviderId);
        IProviderProfileRuntimeLease? profileLease = null;
        Uri? endpointOverride = null;
        try
        {
            var selection = await ProviderProfileAttemptBinding.SelectRuntimeAsync(
                    Descriptor.Binding,
                    request.Operation,
                    _profileSelector,
                    Descriptor.ProviderId,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!selection.IsSuccess)
            {
                sendActivity?.SetFailed("invalid_request", nameof(ProviderFailureKind.InvalidRequest));
                ProviderRequestObservability.RecordRequest("chat", "failed", _timeProvider.GetElapsedTime(sendStarted));
                return await FailAsync(selection.Failure!).ConfigureAwait(false);
            }

            profileLease = selection.Runtime;
            endpointOverride = selection.EndpointBaseAddress;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return await CancelAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException exception)
        {
            return await FailWithKindAsync(
                ProviderFailureKind.Timeout,
                "The credential profile was not selected before its own deadline.",
                exception).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            return await FailWithKindAsync(
                ProviderFailureKind.Authentication,
                "The credential profile could not be selected.",
                exception).ConfigureAwait(false);
        }

        using var httpRequest = CreateHttpRequest(payload, endpointOverride);
        using var deadlineSource = new CancellationTokenSource(
            remaining > _maximumDeadlineDelay ? _maximumDeadlineDelay : remaining, _timeProvider);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadlineSource.Token);

        try
        {
            var sent = await _egress
                .SendAsync(ProviderEgressRequest.ForConversation(Descriptor, request, httpRequest, useStreaming, CreateCredential(profileLease)), cancellationToken)
                .ConfigureAwait(false);
            if (sent is ProviderEgressRefused refused)
            {
                return refused.Failure.Kind is ProviderFailureKind.Cancellation
                    ? await CancelAsync(refused.Failure).ConfigureAwait(false)
                    : await FailAsync(refused.Failure).ConfigureAwait(false);
            }

            var response = ((ProviderEgressSent) sent).Response;
            await using (response.ConfigureAwait(false))
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
                    // A connection reset or truncated body mid-stream is a transport failure, not a caller fault; an
                    // expired body deadline or streamed overrun keeps its own typed classification.
                    return await FailWithKindAsync(
                        ProviderEgressBodyFault.Classify(exception, out var safeMessage),
                        safeMessage,
                        exception).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            if (profileLease is not null)
            {
                await profileLease.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private ProviderEgressCredential? CreateCredential(IProviderProfileRuntimeLease? runtime) =>
        runtime is null ? null : new ProviderEgressCredential(runtime, AuthorizationScheme);

    private HttpRequestMessage CreateHttpRequest(
        JsonObject payload,
        Uri? endpointBaseOverride)
    {
        var targetUri = endpointBaseOverride is null
            ? _profile.ChatCompletionsUri
            : new Uri(ProviderProfileAttemptBinding.NormalizeBaseAddress(endpointBaseOverride), _profile.ChatCompletionsPath);
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, targetUri)
        {
            Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json"),
        };

        foreach (var header in _profile.DefaultRequestHeaders)
        {
            _ = httpRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return httpRequest;
    }

    private async Task<ProviderFailure> BuildHttpFailureAsync(ProviderEgressResponse response, CancellationToken cancellationToken)
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
                providerCode = error?.Error?.Code ?? error?.Error?.Type ?? error?.TopLevelCode;
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
            OpenAIProviderFailureKindMapper.Map(response.StatusCode, providerCode),
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
    private ProviderFailure BuildInterruptedHttpFailure(ProviderEgressResponse response, ProviderFailureKind kind, string safeMessage, Exception? diagnosticCause)
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
