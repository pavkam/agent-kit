// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.WebSearch;

using System.Text.Json.Serialization;

using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Executes bounded search requests against one configured HTTPS endpoint through the AgentKit network boundary,
/// failing closed when authority, enforcement, or required audit is unavailable.
/// </summary>
/// <remarks>
/// <para>
/// One call performs, in order and without any I/O before the first grant is consumed: it validates that the
/// tool-issued egress grant matches this exact attempt; selects the security authority captured by the request's
/// <see cref="ToolExecutionContext"/>; atomically consumes the tool-issued grant with required audit; obtains a
/// separate resolution grant for <see cref="INetworkNameResolver"/>; and obtains a separate send grant for
/// <see cref="INetworkTransport"/>. The resolver and transport consume their own grants, so the lower boundaries
/// re-enforce authority and the tool-issued grant can never substitute for either. Each attempt issues fresh
/// identities and grants; nothing is reused for a retry.
/// </para>
/// <para>
/// The request is a single bodyless <c>GET</c> to the configured endpoint with a bounded response, zero redirects, and
/// the configured data classification. Redirects are never followed: a redirecting endpoint fails the attempt. The
/// query text travels only inside the transport request and is bound into security evidence solely through hashed
/// fingerprints; it never appears in a log, activity tag, metric, or refusal message. The provider owns no
/// <c>HttpClient</c>, handler, or socket.
/// </para>
/// <para>
/// Authority, enforcement, or audit that is missing, throws, or refuses yields <see cref="WebSearchDenied"/>, which the
/// tool projects as a denial with definitely-not-performed side effects. Instances are thread-safe, hold no
/// per-attempt state, and are intended as singletons.
/// </para>
/// </remarks>
public sealed class NetworkWebSearchProvider: IWebSearchProvider
{
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly ISecurityAuthoritySelector _authoritySelector;
    private readonly ISecurityGrantStore _grantStore;
    private readonly ISecurityAuditDispatcher _auditDispatcher;
    private readonly INetworkNameResolver _resolver;
    private readonly INetworkTransport _transport;
    private readonly IIdentifierGenerator<SecurityRequestId> _securityRequestIds;
    private readonly IIdentifierGenerator<NetworkOperationId> _operationIds;
    private readonly IIdentifierGenerator<SecurityEnforcementIntentId> _intentIds;
    private readonly IIdentifierGenerator<SecurityAuditRecordId> _auditRecordIds;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<NetworkWebSearchProvider> _logger;
    private readonly Uri _serviceEndpoint;
    private readonly int _responseByteLimit;
    private readonly TimeSpan _connectTimeout;
    private readonly NetworkDataClassification _classification;

    /// <summary>Initializes the provider over one configured endpoint and one explicit set of security and network collaborators.</summary>
    /// <param name="authoritySelector">Selects the authority a request's captured authorization names.</param>
    /// <param name="grantStore">The authoritative store that atomically consumes the tool-issued grant.</param>
    /// <param name="auditDispatcher">The required audit dispatcher grant consumption must reach before egress.</param>
    /// <param name="resolver">The protected destination resolver.</param>
    /// <param name="transport">The protected request transport.</param>
    /// <param name="securityRequestIds">The replaceable security-request identity source.</param>
    /// <param name="operationIds">The replaceable network-operation identity source.</param>
    /// <param name="intentIds">The replaceable enforcement-intent identity source.</param>
    /// <param name="auditRecordIds">The replaceable audit-record identity source.</param>
    /// <param name="timeProvider">The replaceable clock that owns attempt deadlines.</param>
    /// <param name="options">The validated host options.</param>
    /// <param name="logger">The optional content-free logger; a null value selects a null logger.</param>
    /// <exception cref="ArgumentNullException">A required dependency or the options value is null.</exception>
    /// <exception cref="ArgumentException">The endpoint is missing, not absolute HTTPS, or carries user information or a fragment; or the provider identity is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A bound is not positive or the classification is undefined.</exception>
    public NetworkWebSearchProvider(
        ISecurityAuthoritySelector authoritySelector,
        ISecurityGrantStore grantStore,
        ISecurityAuditDispatcher auditDispatcher,
        INetworkNameResolver resolver,
        INetworkTransport transport,
        IIdentifierGenerator<SecurityRequestId> securityRequestIds,
        IIdentifierGenerator<NetworkOperationId> operationIds,
        IIdentifierGenerator<SecurityEnforcementIntentId> intentIds,
        IIdentifierGenerator<SecurityAuditRecordId> auditRecordIds,
        TimeProvider timeProvider,
        IOptions<NetworkWebSearchProviderOptions> options,
        ILogger<NetworkWebSearchProvider>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(authoritySelector);
        ArgumentNullException.ThrowIfNull(grantStore);
        ArgumentNullException.ThrowIfNull(auditDispatcher);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(securityRequestIds);
        ArgumentNullException.ThrowIfNull(operationIds);
        ArgumentNullException.ThrowIfNull(intentIds);
        ArgumentNullException.ThrowIfNull(auditRecordIds);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        var value = options.Value;
        ArgumentNullException.ThrowIfNull(value, nameof(options));
        ValidateOptions(value);

        _authoritySelector = authoritySelector;
        _grantStore = grantStore;
        _auditDispatcher = auditDispatcher;
        _resolver = resolver;
        _transport = transport;
        _securityRequestIds = securityRequestIds;
        _operationIds = operationIds;
        _intentIds = intentIds;
        _auditRecordIds = auditRecordIds;
        _timeProvider = timeProvider;
        _logger = logger ?? NullLogger<NetworkWebSearchProvider>.Instance;
        ProviderIdentity = value.ProviderId;
        _serviceEndpoint = value.Endpoint!;
        _responseByteLimit = value.MaximumResponseBytes;
        _connectTimeout = value.ConnectTimeout;
        _classification = value.Classification;
        ServiceDestination = new ProtectedResource(
            ProtectedResourceKind.NetworkEndpoint,
            _serviceEndpoint.GetLeftPart(UriPartial.Path));
        EffectAudience = new ComponentId("agentkit.tools.websearch.network");
    }

    private ProviderId ProviderIdentity { get; }

    private ProtectedResource ServiceDestination { get; }

    private ComponentId EffectAudience { get; }

    /// <inheritdoc/>
    public ProviderId ProviderId => ProviderIdentity;

    /// <inheritdoc/>
    public ComponentId SecurityAudience => EffectAudience;

    /// <inheritdoc/>
    public ProtectedResource Destination => ServiceDestination;

    /// <inheritdoc/>
    /// <remarks>
    /// Caller cancellation propagates as <see cref="OperationCanceledException"/>. Expiry of
    /// <see cref="WebSearchRequest.Deadline"/> is a typed <see cref="WebSearchFailed"/> instead. Nothing is resolved,
    /// connected, or transmitted before the tool-issued grant is consumed.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<WebSearchProviderResult> SearchAsync(
        WebSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var stage = "grant";
        if (!ValidateGrant(request))
        {
            return Denied(request, stage, "The egress grant did not match this search attempt.");
        }

        var remaining = request.Deadline - _timeProvider.GetUtcNow();
        if (remaining <= TimeSpan.Zero)
        {
            return Failed(request, stage, "The search attempt exceeded its deadline.");
        }

        using var deadlineSource = new CancellationTokenSource(Clamp(remaining), _timeProvider);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadlineSource.Token);
        try
        {
            return await SearchCoreAsync(request, remaining, value => stage = value, linkedSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            NetworkWebSearchProviderLog.Cancelled(_logger, ProviderIdentity, request.Id, stage);
            throw;
        }
        catch (OperationCanceledException)
        {
            return Failed(request, stage, "The search service did not answer before the attempt deadline.");
        }
    }

    private async Task<WebSearchProviderResult> SearchCoreAsync(
        WebSearchRequest request,
        TimeSpan remaining,
        Action<string> enter,
        CancellationToken cancellationToken)
    {
        enter("destination");
        if (!TryCreateDestination(BuildRequestUri(request), out var destination))
        {
            return Denied(request, "destination", "The search endpoint is not a canonical HTTPS destination.");
        }

        var headers = new NetworkHeaderSet([new NetworkHeader("Accept", "application/json")]);
        var connectTimeout = remaining < _connectTimeout ? remaining : _connectTimeout;
        var bounds = new NetworkBounds(
            new NetworkResolutionBounds(connectTimeout),
            new NetworkRequestBounds(connectTimeout, NetworkBounds.DefaultMaximumRequestBytes),
            new NetworkResponseBounds(remaining, _responseByteLimit, maximumRedirects: 0));

        enter("authority");
        var authorization = request.Context.Authorization;
        var authority = await SelectAuthorityAsync(authorization, cancellationToken).ConfigureAwait(false);
        if (authority is null)
        {
            return Denied(request, "authority", "No security authority was available for the captured authorization context.");
        }

        enter("search-grant");
        var consumed = await ConsumeAsync(request.Grant, cancellationToken).ConfigureAwait(false);
        if (consumed is not null)
        {
            return Denied(request, "search-grant", consumed);
        }

        enter("resolution");
        var operationId = _operationIds.Create();
        var resolutionGrant = await AuthorizeAsync(
            authority,
            request,
            _resolver.SecurityAudience,
            [NetworkSecurityBinding.ResolutionResource(destination)],
            NetworkSecurityBinding.ResolutionFingerprint(operationId, destination, bounds),
            cancellationToken).ConfigureAwait(false);
        if (resolutionGrant.Refusal is { } resolutionRefusal)
        {
            return Denied(request, "resolution", resolutionRefusal);
        }

        var resolution = await _resolver.ResolveAsync(
            new NetworkResolutionRequest(operationId, destination, bounds, resolutionGrant.Grant!),
            cancellationToken).ConfigureAwait(false);
        if (resolution is not NetworkResolved resolved)
        {
            return resolution is NetworkResolutionDenied denied
                ? Denied(request, "resolution", denied.SafeMessage)
                : Failed(request, "resolution", "The search endpoint could not be resolved.");
        }

        enter("send");
        var sendGrant = await AuthorizeAsync(
            authority,
            request,
            _transport.SecurityAudience,
            NetworkSecurityBinding.RequestResources(destination, resolved.Addresses),
            NetworkSecurityBinding.RequestFingerprint(
                operationId,
                NetworkMethod.Get,
                destination,
                headers,
                null,
                bounds,
                resolved.Addresses,
                _classification),
            cancellationToken).ConfigureAwait(false);
        if (sendGrant.Refusal is { } sendRefusal)
        {
            return Denied(request, "send", sendRefusal);
        }

        var send = await _transport.SendAsync(
            new NetworkRequest(
                operationId,
                NetworkMethod.Get,
                destination,
                headers,
                content: null,
                bounds,
                resolved.Addresses,
                _classification,
                sendGrant.Grant!),
            cancellationToken).ConfigureAwait(false);
        if (send is not NetworkResponseReceived received)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return RefuseSend(request, send);
        }

        enter("response");
        await using var response = received.Response;
        var status = response.Metadata.StatusCode;
        NetworkWebSearchProviderLog.Answered(_logger, ProviderIdentity, request.Id, status);
        return status is < 200 or > 299
            ? Failed(request, "response", "The search service returned an error.")
            : await ReadAsync(request, response, cancellationToken).ConfigureAwait(false);
    }

    private async Task<WebSearchProviderResult> ReadAsync(
        WebSearchRequest request,
        INetworkResponse response,
        CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream(capacity: Math.Min(_responseByteLimit, 65_536));
        var chunk = new byte[8192];
        try
        {
            while (true)
            {
                var read = await response.Content.ReadAsync(chunk, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                if (buffer.Length + read > _responseByteLimit)
                {
                    return Failed(request, "response", "The search response exceeded the configured size limit.");
                }

                buffer.Write(chunk, 0, read);
            }
        }
        catch (NetworkResponseTooLargeException)
        {
            return Failed(request, "response", "The search response exceeded the configured size limit.");
        }
        catch (NetworkResponseTimedOutException)
        {
            return Failed(request, "response", "The search service did not answer before the attempt deadline.");
        }
        catch (IOException)
        {
            return Failed(request, "response", "The search response could not be read.");
        }

        WireResponse? payload;
        try
        {
            payload = JsonSerializer.Deserialize<WireResponse>(buffer.ToArray(), _jsonOptions);
        }
        catch (JsonException)
        {
            return Failed(request, "response", "The search service returned an invalid response.");
        }

        if (payload?.Results is null)
        {
            return Failed(request, "response", "The search service returned an invalid response.");
        }

        var items = ImmutableArray.CreateBuilder<WebSearchItem>(Math.Min(payload.Results.Count, request.MaximumResults));
        foreach (var entry in payload.Results.Take(request.MaximumResults))
        {
            if (string.IsNullOrWhiteSpace(entry.Title)
                || string.IsNullOrWhiteSpace(entry.Url)
                || string.IsNullOrWhiteSpace(entry.Snippet))
            {
                return Failed(request, "response", "The search service returned an invalid result entry.");
            }

            if (!Uri.TryCreate(entry.Url, UriKind.Absolute, out var uri))
            {
                return Failed(request, "response", "The search service returned an invalid result URL.");
            }

            DateTimeOffset? publishedAt = null;
            if (!string.IsNullOrWhiteSpace(entry.PublishedAt)
                && DateTimeOffset.TryParse(entry.PublishedAt, out var parsed))
            {
                publishedAt = parsed.ToUniversalTime();
            }

            items.Add(new WebSearchItem(entry.Title, uri, entry.Snippet, publishedAt));
        }

        var complete = payload.Complete && items.Count == payload.Results.Count;
        return new WebSearchSucceeded(request.Id, items.ToImmutable(), complete);
    }

    private WebSearchProviderResult RefuseSend(WebSearchRequest request, NetworkSendResult result) => result switch
    {
        NetworkDenied denied => Denied(request, "send", denied.SafeMessage),
        NetworkRequestFailed { Kind: NetworkFailureKind.Timeout } =>
            Failed(request, "send", "The search service did not answer before the attempt deadline."),
        NetworkRequestFailed => Failed(request, "send", "The search service did not answer."),
        NetworkCancelled => Failed(request, "send", "The search request was cancelled."),
        NetworkRedirectReceived or NetworkRedirectLimitExceeded =>
            Failed(request, "send", "The search endpoint answered with a redirect, which is never followed."),
        NetworkResponseLimitExceeded =>
            Failed(request, "send", "The search response exceeded the configured size limit."),
        _ => Failed(request, "send", "The transport returned an unsupported result."),
    };

    private async ValueTask<AuthorityActivation?> SelectAuthorityAsync(
        SecurityAuthorizationContext authorization,
        CancellationToken cancellationToken)
    {
        try
        {
            var selection = await _authoritySelector.SelectAsync(authorization, cancellationToken).ConfigureAwait(false);
            return selection is SecurityAuthoritySelected selected && selected.Authorization == authorization
                ? new AuthorityActivation(selected.Authority)
                : null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return null;
        }
    }

    private async ValueTask<GrantAttempt> AuthorizeAsync(
        AuthorityActivation activation,
        WebSearchRequest request,
        ComponentId audience,
        ImmutableArray<ProtectedResource> resources,
        InputFingerprint fingerprint,
        CancellationToken cancellationToken)
    {
        var authorization = request.Context.Authorization;
        SecurityDecision decision;
        try
        {
            decision = await activation.Authority.AuthorizeAsync(
                new SecurityRequest(
                    _securityRequestIds.Create(),
                    authorization.Scope,
                    request.Context.ToolCallId,
                    authorization.Identity,
                    authorization,
                    audience,
                    SecurityOperationKind.Network,
                    SecurityEffect.Egress,
                    resources,
                    fingerprint,
                    request.Deadline),
                hooks: null,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return GrantAttempt.Refused("The security authority was unavailable, so search egress was not authorized.");
        }

        return decision switch
        {
            SecurityAllowed allowed => GrantAttempt.Allowed(allowed.Grant),
            SecurityApprovalRequired => GrantAttempt.Refused("Search egress requires an approval that cannot be awaited at this boundary."),
            SecurityDenied denied => GrantAttempt.Refused(denied.Denial.SafeMessage),
            _ => GrantAttempt.Refused("Search egress was denied by the security authority."),
        };
    }

    private async ValueTask<string?> ConsumeAsync(SecurityGrant grant, CancellationToken cancellationToken)
    {
        var enforcement = NetworkWebSearchEnforcement.Create(grant);
        var intent = new SecurityEnforcementIntent(_intentIds.Create(), null);
        try
        {
            return await SecurityGrantConsumptionHostOperations.ConsumeWithRequiredAuditAsync(
                grant,
                enforcement,
                intent,
                _grantStore,
                _auditDispatcher,
                _auditRecordIds,
                _timeProvider,
                NetworkWebSearchEnforcement.IsFreshExact,
                NetworkWebSearchEnforcement.DenialMessage,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return "Search egress enforcement was unavailable, so the grant was not consumed.";
        }
    }

    private bool ValidateGrant(WebSearchRequest request)
    {
        var expected = WebSearchSecurityBinding.Fingerprint(
            request.Id,
            ProviderIdentity,
            ServiceDestination,
            request.Query,
            request.Domains,
            request.Freshness,
            request.MaximumResults,
            request.Deadline);
        return request.Grant.Audience == EffectAudience
            && request.Grant.Kind == SecurityOperationKind.Network
            && request.Grant.Effect == SecurityEffect.Egress
            && request.Grant.Resources.SequenceEqual([ServiceDestination])
            && request.Grant.InputFingerprint == expected;
    }

    private WebSearchDenied Denied(WebSearchRequest request, string stage, string safeMessage)
    {
        NetworkWebSearchProviderLog.Denied(_logger, ProviderIdentity, request.Id, stage);
        return new WebSearchDenied(request.Id, safeMessage);
    }

    private WebSearchFailed Failed(WebSearchRequest request, string stage, string safeMessage)
    {
        NetworkWebSearchProviderLog.Failed(_logger, ProviderIdentity, request.Id, stage);
        return new WebSearchFailed(request.Id, safeMessage);
    }

    private Uri BuildRequestUri(WebSearchRequest request)
    {
        var builder = new UriBuilder(_serviceEndpoint);
        var query = new List<string>
        {
            $"q={Uri.EscapeDataString(request.Query)}",
            $"maximum_results={request.MaximumResults}",
            $"freshness={FreshnessToken(request.Freshness)}",
        };
        if (!request.Domains.IsDefaultOrEmpty)
        {
            query.Add($"domains={Uri.EscapeDataString(string.Join(',', request.Domains.Select(static d => d.Value)))}");
        }

        var separator = string.IsNullOrEmpty(builder.Query) ? "?" : "&";
        builder.Query = string.IsNullOrEmpty(builder.Query)
            ? string.Join('&', query)
            : builder.Query.TrimStart('?') + separator + string.Join('&', query);
        return builder.Uri;
    }

    private static bool TryCreateDestination(Uri uri, out NetworkDestination destination)
    {
        destination = null!;
        if (uri.Scheme is not "https"
            || string.IsNullOrWhiteSpace(uri.IdnHost)
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Fragment))
        {
            return false;
        }

        var route = uri.GetComponents(UriComponents.PathAndQuery, UriFormat.UriEscaped);
        try
        {
            destination = new NetworkDestination(
                uri.Scheme,
                new NormalizedHost(uri.IdnHost),
                uri.Port,
                string.IsNullOrEmpty(route) ? NetworkRoute.Root : new NetworkRoute($"/{route.TrimStart('/')}"));
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static TimeSpan Clamp(TimeSpan remaining)
    {
        var maximum = TimeSpan.FromMilliseconds(uint.MaxValue - 1);
        return remaining > maximum ? maximum : remaining;
    }

    private static string FreshnessToken(WebSearchFreshness freshness) => freshness switch
    {
        WebSearchFreshness.Any => "any",
        WebSearchFreshness.Day => "day",
        WebSearchFreshness.Week => "week",
        WebSearchFreshness.Month => "month",
        WebSearchFreshness.Year => "year",
        _ => throw new ArgumentOutOfRangeException(nameof(freshness), freshness, "The freshness value is undefined."),
    };

    private static void ValidateOptions(NetworkWebSearchProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options.Endpoint, nameof(options));
        if (!options.Endpoint.IsAbsoluteUri
            || options.Endpoint.Scheme is not "https"
            || !string.IsNullOrEmpty(options.Endpoint.UserInfo)
            || !string.IsNullOrEmpty(options.Endpoint.Fragment))
        {
            throw new ArgumentException(
                "Endpoint must be an absolute HTTPS URI without user information or a fragment.",
                nameof(options));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaximumResponseBytes, nameof(options));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(options.ConnectTimeout, TimeSpan.Zero, nameof(options));
        ArgumentOutOfRangeException.ThrowIfUndefined(options.Classification, nameof(options));
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ProviderId.Value, nameof(options));
    }

    private sealed record AuthorityActivation(ISecurityAuthority Authority);

    private readonly record struct GrantAttempt(SecurityGrant? Grant, string? Refusal)
    {
        internal static GrantAttempt Allowed(SecurityGrant grant) => new(grant, null);

        internal static GrantAttempt Refused(string refusal) => new(null, refusal);
    }

    private sealed class WireResponse
    {
        public bool Complete { get; init; }
        public List<WireResult>? Results { get; init; }
    }

    private sealed class WireResult
    {
        public string? Title { get; init; }
        public string? Url { get; init; }
        public string? Snippet { get; init; }
        [JsonPropertyName("published_at")]
        public string? PublishedAt { get; init; }
    }
}
