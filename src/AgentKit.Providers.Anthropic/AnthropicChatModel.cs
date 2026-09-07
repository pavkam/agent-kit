// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Anthropic;

using System.Net;
using System.Net.Http;

using AgentKit.Providers.Anthropic.Wire;

/// <summary>
/// The Anthropic Claude conversational <see cref="ILlmModel"/>, performing
/// translation, credential resolution, transport, and stream parsing for
/// the Anthropic Messages API directly.
/// </summary>
/// <remarks>
/// Unlike the OpenAI-compatible family, Anthropic has exactly one AgentKit
/// integration today, so this class implements <see cref="ILlmModel"/>
/// directly rather than deriving from a shared protocol-family base class.
/// <see cref="IAnthropicMessageTranslator"/> and
/// <see cref="IAnthropicMessageStreamParser"/> remain independent, publicly
/// injectable collaborators so a future platform broker that must reuse
/// canonical Claude message semantics under its own authentication and
/// transport (for example, Amazon Bedrock or Google Vertex AI, per
/// Anthropic's own cross-platform guidance) can reuse them without
/// depending on this class's direct-HTTP transport.
/// </remarks>
public sealed class AnthropicLlmModel: ILlmModel
{
    private readonly ModelDescriptor _descriptor;
    private readonly AnthropicProviderOptions _options;
    private readonly IAnthropicMessageTranslator _translator;
    private readonly IAnthropicMessageStreamParser _streamParser;
    private readonly IProviderCredentialSource _credentials;
    private readonly HttpClient _httpClient;
    private readonly TimeProvider _timeProvider;

    /// <summary>Initializes a new instance of the <see cref="AnthropicLlmModel"/> class.</summary>
    /// <param name="descriptor">The descriptor of the model this instance serves.</param>
    /// <param name="options">The validated Anthropic provider options.</param>
    /// <param name="translator">Translates provider-neutral requests into Anthropic Messages request bodies.</param>
    /// <param name="streamParser">Parses Anthropic Messages responses into normalized events.</param>
    /// <param name="credentials">Resolves the current credential for <paramref name="descriptor"/>'s provider.</param>
    /// <param name="httpClient">The HTTP client used to send requests.</param>
    /// <param name="timeProvider">The clock used for deadline and credential-expiry evaluation.</param>
    /// <exception cref="ArgumentNullException">Any parameter is null.</exception>
    public AnthropicLlmModel(
        ModelDescriptor descriptor,
        AnthropicProviderOptions options,
        IAnthropicMessageTranslator translator,
        IAnthropicMessageStreamParser streamParser,
        IProviderCredentialSource credentials,
        HttpClient httpClient,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(translator);
        ArgumentNullException.ThrowIfNull(streamParser);
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(timeProvider);

        Alias = descriptor.Alias;
        _descriptor = descriptor;
        _options = options;
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

        var authorization = AnthropicAuthorizationHeaderFactory.Create(credential, _descriptor.ProviderId, _timeProvider);
        if (authorization is AnthropicAuthorizationDenied denied)
        {
            return await FailAsync(denied.Failure).ConfigureAwait(false);
        }

        var granted = (AnthropicAuthorizationGranted) authorization;
        var useStreaming = _options.PreferStreaming && _descriptor.Capabilities.SupportsStreaming;

        JsonObject payload;
        try
        {
            payload = _translator.Translate(request, _options, useStreaming);
        }
        catch (NotSupportedException exception)
        {
            return await FailWithKindAsync(
                ProviderFailureKind.InvalidRequest,
                "The request could not be translated for the Anthropic Messages wire format.",
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

            var parseContext = new AnthropicResponseParseContext(
                requestId,
                _descriptor.ProviderId,
                _descriptor.ApiFamily,
                _descriptor.ModelId,
                deploymentId: null,
                TryReadProviderRequestId(response));

            try
            {
                var body = await response.Content.ReadAsStreamAsync(linkedSource.Token).ConfigureAwait(false);
                await using (body.ConfigureAwait(false))
                {
                    return useStreaming
                        ? await _streamParser
                            .ParseStreamingAsync(body, parseContext, observer, linkedSource.Token)
                            .ConfigureAwait(false)
                        : await _streamParser
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

    private HttpRequestMessage CreateHttpRequest(JsonObject payload, AnthropicAuthorizationGranted authorization)
    {
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, AnthropicProviderDefaults.BuildMessagesUri(_options))
        {
            Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json"),
        };

        _ = httpRequest.Headers.TryAddWithoutValidation(authorization.HeaderName, authorization.HeaderValue);
        _ = httpRequest.Headers.TryAddWithoutValidation("anthropic-version", _options.AnthropicVersion);

        return httpRequest;
    }

    private async Task<ProviderFailure> BuildHttpFailureAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        string? safeMessage = null;
        string? errorType = null;

        try
        {
            var body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using (body.ConfigureAwait(false))
            {
                var envelope = await JsonSerializer
                    .DeserializeAsync<AnthropicErrorEnvelopeDto>(body, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                safeMessage = envelope?.Error?.Message;
                errorType = envelope?.Error?.Type;
            }
        }
        catch (JsonException)
        {
            // The error body was not valid JSON; fall back to a generic message below.
        }

        var kind = errorType is not null
            ? AnthropicErrorMapping.MapErrorType(errorType)
            : MapStatusCode(response.StatusCode);

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

    private static ProviderFailureKind MapStatusCode(HttpStatusCode statusCode) =>
        statusCode switch
        {
            HttpStatusCode.Unauthorized => ProviderFailureKind.Authentication,
            HttpStatusCode.Forbidden => ProviderFailureKind.Authorization,
            HttpStatusCode.TooManyRequests => ProviderFailureKind.Throttling,
            HttpStatusCode.BadRequest or HttpStatusCode.NotFound or HttpStatusCode.RequestEntityTooLarge =>
                ProviderFailureKind.InvalidRequest,
            (HttpStatusCode) 529 => ProviderFailureKind.Unavailable,
            var code when (int) code >= 500 => ProviderFailureKind.Unavailable,
            HttpStatusCode.Continue => throw new NotImplementedException(),
            HttpStatusCode.SwitchingProtocols => throw new NotImplementedException(),
            HttpStatusCode.Processing => throw new NotImplementedException(),
            HttpStatusCode.EarlyHints => throw new NotImplementedException(),
            HttpStatusCode.OK => throw new NotImplementedException(),
            HttpStatusCode.Created => throw new NotImplementedException(),
            HttpStatusCode.Accepted => throw new NotImplementedException(),
            HttpStatusCode.NonAuthoritativeInformation => throw new NotImplementedException(),
            HttpStatusCode.NoContent => throw new NotImplementedException(),
            HttpStatusCode.ResetContent => throw new NotImplementedException(),
            HttpStatusCode.PartialContent => throw new NotImplementedException(),
            HttpStatusCode.MultiStatus => throw new NotImplementedException(),
            HttpStatusCode.AlreadyReported => throw new NotImplementedException(),
            HttpStatusCode.IMUsed => throw new NotImplementedException(),
            HttpStatusCode.Ambiguous => throw new NotImplementedException(),
            HttpStatusCode.Moved => throw new NotImplementedException(),
            HttpStatusCode.Found => throw new NotImplementedException(),
            HttpStatusCode.RedirectMethod => throw new NotImplementedException(),
            HttpStatusCode.NotModified => throw new NotImplementedException(),
            HttpStatusCode.UseProxy => throw new NotImplementedException(),
            HttpStatusCode.Unused => throw new NotImplementedException(),
            HttpStatusCode.RedirectKeepVerb => throw new NotImplementedException(),
            HttpStatusCode.PermanentRedirect => throw new NotImplementedException(),
            HttpStatusCode.PaymentRequired => throw new NotImplementedException(),
            HttpStatusCode.MethodNotAllowed => throw new NotImplementedException(),
            HttpStatusCode.NotAcceptable => throw new NotImplementedException(),
            HttpStatusCode.ProxyAuthenticationRequired => throw new NotImplementedException(),
            HttpStatusCode.RequestTimeout => throw new NotImplementedException(),
            HttpStatusCode.Conflict => throw new NotImplementedException(),
            HttpStatusCode.Gone => throw new NotImplementedException(),
            HttpStatusCode.LengthRequired => throw new NotImplementedException(),
            HttpStatusCode.PreconditionFailed => throw new NotImplementedException(),
            HttpStatusCode.RequestUriTooLong => throw new NotImplementedException(),
            HttpStatusCode.UnsupportedMediaType => throw new NotImplementedException(),
            HttpStatusCode.RequestedRangeNotSatisfiable => throw new NotImplementedException(),
            HttpStatusCode.ExpectationFailed => throw new NotImplementedException(),
            HttpStatusCode.MisdirectedRequest => throw new NotImplementedException(),
            HttpStatusCode.UnprocessableEntity => throw new NotImplementedException(),
            HttpStatusCode.Locked => throw new NotImplementedException(),
            HttpStatusCode.FailedDependency => throw new NotImplementedException(),
            HttpStatusCode.UpgradeRequired => throw new NotImplementedException(),
            HttpStatusCode.PreconditionRequired => throw new NotImplementedException(),
            HttpStatusCode.RequestHeaderFieldsTooLarge => throw new NotImplementedException(),
            HttpStatusCode.UnavailableForLegalReasons => throw new NotImplementedException(),
            HttpStatusCode.InternalServerError => throw new NotImplementedException(),
            HttpStatusCode.NotImplemented => throw new NotImplementedException(),
            HttpStatusCode.BadGateway => throw new NotImplementedException(),
            HttpStatusCode.ServiceUnavailable => throw new NotImplementedException(),
            HttpStatusCode.GatewayTimeout => throw new NotImplementedException(),
            HttpStatusCode.HttpVersionNotSupported => throw new NotImplementedException(),
            HttpStatusCode.VariantAlsoNegotiates => throw new NotImplementedException(),
            HttpStatusCode.InsufficientStorage => throw new NotImplementedException(),
            HttpStatusCode.LoopDetected => throw new NotImplementedException(),
            HttpStatusCode.NotExtended => throw new NotImplementedException(),
            HttpStatusCode.NetworkAuthenticationRequired => throw new NotImplementedException(),
            _ => ProviderFailureKind.Unknown,
        };

    private static ProviderRequestId? TryReadProviderRequestId(HttpResponseMessage response) =>
        response.Headers.TryGetValues("request-id", out var values) && values.FirstOrDefault() is { Length: > 0 } value
            ? new ProviderRequestId(value)
            : null;
}
