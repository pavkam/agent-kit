// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenRouter;

using System.Net.Http;

using AgentKit.Providers;
using AgentKit.Providers.Egress;
using AgentKit.Providers.Http;

/// <summary>The OpenRouter rerank <see cref="IReranker"/> implementation.</summary>
public sealed class OpenRouterReranker: IReranker
{
    private readonly RerankerDescriptor _descriptor;
    private readonly OpenRouterProviderOptions _options;
    private readonly IOpenRouterRerankRequestTranslator _translator;
    private readonly IOpenRouterRerankResponseParser _responseParser;
    private readonly IProviderCredentialSource _credentials;
    private readonly ProviderEgress _egress;
    private readonly TimeProvider _timeProvider;
    private readonly IProviderProfileRuntimeSelector? _profileSelector;

    /// <summary>Initializes a new instance of the <see cref="OpenRouterReranker"/> class.</summary>
    /// <param name="descriptor">The reranker descriptor this instance serves.</param>
    /// <param name="options">The validated OpenRouter provider options.</param>
    /// <param name="translator">Translates rerank requests into OpenRouter wire bodies.</param>
    /// <param name="responseParser">Parses OpenRouter rerank responses.</param>
    /// <param name="credentials">Resolves credentials for the OpenRouter provider.</param>
    /// <param name="egress">The provider-egress boundary every attempt sends through.</param>
    /// <param name="timeProvider">The clock used for deadlines and credential expiry.</param>
    /// <param name="profileSelector">The optional profile runtime selector.</param>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    public OpenRouterReranker(
        RerankerDescriptor descriptor,
        OpenRouterProviderOptions options,
        IOpenRouterRerankRequestTranslator translator,
        IOpenRouterRerankResponseParser responseParser,
        IProviderCredentialSource credentials,
        ProviderEgress egress,
        TimeProvider timeProvider,
        IProviderProfileRuntimeSelector? profileSelector = null)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(translator);
        ArgumentNullException.ThrowIfNull(responseParser);
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(egress);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _descriptor = descriptor;
        _options = options;
        _translator = translator;
        _responseParser = responseParser;
        _credentials = credentials;
        _egress = egress;
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
                ProviderAuthorizationScheme.BearerToken);
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
                    "The request could not be translated for the OpenRouter rerank wire format.",
                    exception);
            }

            using var httpRequest = CreateHttpRequest(payload, granted, endpointOverride);
            using var deadlineSource = new CancellationTokenSource(remaining, _timeProvider);
            using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadlineSource.Token);

            var sent = await _egress
                .SendAsync(ProviderEgressRequest.ForReranking(_descriptor, request, httpRequest), cancellationToken)
                .ConfigureAwait(false);
            if (sent is ProviderEgressRefused refused)
            {
                return new RerankModelFailed(refused.Failure);
            }

            var response = ((ProviderEgressSent) sent).Response;

            await using (response.ConfigureAwait(false))
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
                            $"The OpenRouter rerank request failed with HTTP status {(int) response.StatusCode}.",
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
            OpenRouterProviderDefaults.BuildRerankUri(_options, endpointBaseOverride))
        {
            Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json"),
        };

        authorization.Apply(httpRequest.Headers);

        foreach (var header in OpenRouterProviderDefaults.CreateProfile(_options).DefaultRequestHeaders)
        {
            _ = httpRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

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
