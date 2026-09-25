// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Cohere;

using System.Net.Http;

using AgentKit.Providers;
using AgentKit.Providers.Http;

/// <summary>The Cohere v2 rerank <see cref="IReranker"/> implementation.</summary>
public sealed class CohereReranker: IReranker
{
    private readonly RerankerDescriptor _descriptor;
    private readonly CohereProviderOptions _options;
    private readonly ICohereRerankRequestTranslator _translator;
    private readonly ICohereRerankResponseParser _responseParser;
    private readonly IProviderCredentialSource _credentials;
    private readonly HttpClient _httpClient;
    private readonly TimeProvider _timeProvider;
    private readonly IProviderProfileRuntimeSelector? _profileSelector;

    /// <summary>Initializes a new instance of the <see cref="CohereReranker"/> class.</summary>
    /// <param name="descriptor">The reranker descriptor this instance serves.</param>
    /// <param name="options">The validated Cohere provider options.</param>
    /// <param name="translator">Translates rerank requests into Cohere wire bodies.</param>
    /// <param name="responseParser">Parses Cohere rerank responses.</param>
    /// <param name="credentials">Resolves credentials for the Cohere provider.</param>
    /// <param name="httpClient">The HTTP client used to send requests.</param>
    /// <param name="timeProvider">The clock used for deadlines and credential expiry.</param>
    /// <param name="profileSelector">The optional profile runtime selector.</param>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    public CohereReranker(
        RerankerDescriptor descriptor,
        CohereProviderOptions options,
        ICohereRerankRequestTranslator translator,
        ICohereRerankResponseParser responseParser,
        IProviderCredentialSource credentials,
        HttpClient httpClient,
        TimeProvider timeProvider,
        IProviderProfileRuntimeSelector? profileSelector = null)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(translator);
        ArgumentNullException.ThrowIfNull(responseParser);
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _descriptor = descriptor;
        _options = options;
        _translator = translator;
        _responseParser = responseParser;
        _credentials = credentials;
        _httpClient = httpClient;
        _timeProvider = timeProvider;
        _profileSelector = profileSelector;
    }

    /// <inheritdoc/>
    public RerankerAlias Alias => _descriptor.Alias;

    /// <inheritdoc/>
    public async Task<RerankModelResult> RerankAsync(RerankModelRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Selection.Model.Alias != _descriptor.Alias)
        {
            return Failed(
                ProviderFailureKind.InvalidRequest,
                "The rerank request selected a different reranker alias than this adapter serves.");
        }

        var remaining = request.Deadline - _timeProvider.GetUtcNow();
        if (remaining <= TimeSpan.Zero)
        {
            return Failed(ProviderFailureKind.Timeout, "The request deadline had already elapsed before the attempt could be sent.");
        }

        var sendStarted = _timeProvider.GetTimestamp();
        using var sendActivity = ProviderRequestObservability.StartRerankSend(_descriptor.ProviderId);
        IProviderProfileRuntimeLease? profileLease = null;
        IProviderCredentialSource credentialSource;
        Uri? endpointOverride;
        try
        {
            var binding = await ProviderProfileAttemptBinding.ResolveCredentialSourceAsync(
                    _descriptor.Binding,
                    request.Operation,
                    _credentials,
                    _profileSelector,
                    _descriptor.ProviderId,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!binding.IsSuccess)
            {
                _ = sendActivity?.SetStatus(ActivityStatusCode.Error, binding.Failure!.SafeMessage);
                ProviderRequestObservability.RecordRequest("rerank", "failed", _timeProvider.GetElapsedTime(sendStarted));
                return new RerankModelFailed(binding.Failure!);
            }

            profileLease = binding.Lease;
            credentialSource = binding.CredentialSource!;
            endpointOverride = binding.EndpointBaseAddress;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Failed(ProviderFailureKind.Cancellation, "The attempt was cancelled.");
        }

        try
        {
            ProviderCredential credential;
            try
            {
                credential = await credentialSource.GetCredentialAsync(_descriptor.ProviderId, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return Failed(ProviderFailureKind.Cancellation, "The attempt was cancelled.");
            }

            var authorization = ProviderAuthorizationHeaderFactory.Create(
                credential,
                _descriptor.ProviderId,
                _timeProvider,
                CohereProviderDefaults.AuthorizationScheme);
            if (authorization is ProviderAuthorizationDenied denied)
            {
                return new RerankModelFailed(denied.Failure);
            }

            var granted = (ProviderAuthorizationGranted) authorization;

            JsonObject payload;
            try
            {
                payload = _translator.Translate(request, _descriptor);
            }
            catch (NotSupportedException exception)
            {
                return Failed(
                    ProviderFailureKind.InvalidRequest,
                    "The request could not be translated for the Cohere rerank wire format.",
                    exception);
            }

            using var httpRequest = CreateHttpRequest(payload, granted, endpointOverride);
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
                return Failed(ProviderFailureKind.Cancellation, "The attempt was cancelled.");
            }
            catch (OperationCanceledException exception) when (deadlineSource.IsCancellationRequested)
            {
                return Failed(ProviderFailureKind.Timeout, "The request did not complete before its deadline.", exception);
            }
            catch (OperationCanceledException exception)
            {
                return Failed(ProviderFailureKind.Timeout, "The transport timed out before the provider responded.", exception);
            }
            catch (HttpRequestException exception)
            {
                return Failed(ProviderFailureKind.Unavailable, "The provider could not be reached.", exception);
            }

            using (response)
            {
                if (!response.IsSuccessStatusCode)
                {
                    return new RerankModelFailed(
                        new ProviderFailure(
                            HttpStatusFailureKindMapper.Map(response.StatusCode),
                            _descriptor.ProviderId,
                            requestId: null,
                            (int) response.StatusCode,
                            providerCode: null,
                            retryAfter: RetryAfterResolver.Resolve(response.Headers, _timeProvider),
                            $"The Cohere rerank request failed with HTTP status {(int) response.StatusCode}.",
                            diagnosticCause: null,
                            ExtensionData.Empty));
                }

                var body = await response.Content.ReadAsStreamAsync(linkedSource.Token).ConfigureAwait(false);
                await using (body.ConfigureAwait(false))
                {
                    return await _responseParser
                        .ParseAsync(body, request.Request.Documents, _descriptor.ProviderId, linkedSource.Token)
                        .ConfigureAwait(false);
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

    private HttpRequestMessage CreateHttpRequest(
        JsonObject payload,
        ProviderAuthorizationGranted authorization,
        Uri? endpointBaseOverride)
    {
        var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            CohereProviderDefaults.BuildRerankUri(_options, endpointBaseOverride))
        {
            Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json"),
        };

        authorization.Apply(httpRequest.Headers);
        return httpRequest;
    }

    private RerankModelFailed Failed(ProviderFailureKind kind, string safeMessage, Exception? cause = null) =>
        new(
            new ProviderFailure(
                kind,
                _descriptor.ProviderId,
                requestId: null,
                statusCode: null,
                providerCode: null,
                retryAfter: null,
                safeMessage,
                cause,
                ExtensionData.Empty));
}
