// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Egress;

using System.Net.Http;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

/// <summary>
/// Sends one prepared provider wire request through the AgentKit network boundary under a per-attempt provider-egress
/// grant, failing closed when authority, enforcement, or required audit is unavailable.
/// </summary>
/// <remarks>
/// <para>
/// Every first-party provider adapter sends through this boundary instead of an <c>HttpClient</c>. One call performs,
/// in order and without any I/O before the first grant is consumed: it selects the security authority captured by the
/// request's <see cref="ProtectedSemanticOperationContext"/>; obtains and atomically consumes a provider-egress grant
/// bound to the exact endpoint and credential profile binding, model, attempt, canonical destination, payload hash,
/// and classification (audience <see cref="SecurityAudience"/>, consumed with required audit); obtains a separate
/// resolution grant for <see cref="INetworkNameResolver"/>; and obtains a separate send grant for
/// <see cref="INetworkTransport"/>. The resolver and transport consume their own grants, so the lower boundaries
/// re-enforce authority and a provider-egress grant can never substitute for either.
/// </para>
/// <para>
/// The body is frozen into immutable bytes before the first grant so every fingerprint covers the bytes actually sent.
/// Credential headers travel only inside the transport request and are bound solely by the transport's hashed header
/// fingerprint; they never appear in a security request, an audit record, a log, or a refusal. Redirects are never
/// followed: a redirecting provider endpoint refuses the attempt, so credentials cannot reach another origin. The
/// network leaf keeps connection pooling and pinned-address behavior.
/// </para>
/// <para>
/// Instances are thread-safe, hold no per-attempt state, and are intended as engine-wide singletons. Each attempt
/// issues fresh identities and grants; nothing is reused for a retry or fallback.
/// </para>
/// </remarks>
public sealed class ProviderEgress
{
    /// <summary>
    /// The largest delay <see cref="CancellationTokenSource(TimeSpan, TimeProvider)"/> accepts, so a far-future deadline
    /// is clamped instead of faulting the attempt.
    /// </summary>
    private static readonly TimeSpan _maximumDeadlineDelay = TimeSpan.FromMilliseconds(uint.MaxValue - 1);

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
    private readonly ProviderEgressOptions _options;
    private readonly ILogger<ProviderEgress> _logger;

    /// <summary>Initializes provider egress over one explicit set of security and network collaborators.</summary>
    /// <param name="authoritySelector">Selects the authority a request's captured authorization names.</param>
    /// <param name="grantStore">The authoritative store that atomically consumes the provider-egress grant.</param>
    /// <param name="auditDispatcher">The required audit dispatcher grant consumption must reach before egress.</param>
    /// <param name="resolver">The protected destination resolver.</param>
    /// <param name="transport">The protected request transport.</param>
    /// <param name="securityRequestIds">The replaceable security-request identity source.</param>
    /// <param name="operationIds">The replaceable network-operation identity source.</param>
    /// <param name="intentIds">The replaceable enforcement-intent identity source.</param>
    /// <param name="auditRecordIds">The replaceable audit-record identity source.</param>
    /// <param name="timeProvider">The clock that owns attempt deadlines.</param>
    /// <param name="options">The validated host bounds and classification.</param>
    /// <param name="logger">The optional content-free logger; a null value selects a null logger.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A configured bound is not positive or the classification is undefined.</exception>
    public ProviderEgress(
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
        IOptions<ProviderEgressOptions> options,
        ILogger<ProviderEgress>? logger = null)
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
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value.ConnectTimeout, TimeSpan.Zero, nameof(options));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value.MaximumRequestBytes, nameof(options));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value.MaximumResponseBytes, nameof(options));
        ArgumentOutOfRangeException.ThrowIfUndefined(value.Classification, nameof(options));

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
        _options = new ProviderEgressOptions
        {
            ConnectTimeout = value.ConnectTimeout,
            MaximumRequestBytes = value.MaximumRequestBytes,
            MaximumResponseBytes = value.MaximumResponseBytes,
            Classification = value.Classification,
        };
        _logger = logger ?? NullLogger<ProviderEgress>.Instance;
    }

    /// <summary>Gets the exact enforcement audience that consumes provider-egress grants.</summary>
    /// <value>A stable component identity distinct from the resolver's and the transport's audiences.</value>
    public static ComponentId SecurityAudience => ProviderEgressBinding.Audience;

    /// <summary>Authorizes and sends one prepared provider request.</summary>
    /// <param name="request">The attempt identity and prepared wire message.</param>
    /// <param name="cancellationToken">
    /// A token that cancels the attempt at authorization, resolution, upload, or response-header wait. Cancellation
    /// returns a <see cref="ProviderFailureKind.Cancellation"/> refusal rather than throwing.
    /// </param>
    /// <returns>
    /// An owned <see cref="ProviderEgressSent"/> response handle, or a <see cref="ProviderEgressRefused"/> carrying the
    /// normalized failure. Nothing is resolved, connected, or transmitted before the provider-egress grant is consumed.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <remarks>
    /// The response timeout the transport enforces, including while the body streams, is the time remaining until
    /// <see cref="ProviderEgressRequest.Deadline"/>. The caller consumes the body incrementally and disposes the
    /// response asynchronously exactly once.
    /// </remarks>
    public async ValueTask<ProviderEgressResult> SendAsync(
        ProviderEgressRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var started = _timeProvider.GetTimestamp();
        using var activity = AgentKitDiagnostics.Activities.StartActivity(AgentKitActivityNames.ProviderEgress);
        _ = activity?.SetTag(AgentKitTagNames.ProviderName, request.ProviderId.ToString());
        _ = activity?.SetTag(AgentKitTagNames.ProviderOperation, OperationTag(request.Kind));

        var stage = "authority";
        ProviderEgressResult result;
        var remaining = request.Deadline - _timeProvider.GetUtcNow();
        if (remaining <= TimeSpan.Zero)
        {
            result = Refuse(request, ProviderFailureKind.Timeout, "The request deadline had already elapsed before the attempt could be sent.", stage);
        }
        else
        {
            using var deadlineSource = new CancellationTokenSource(
                remaining > _maximumDeadlineDelay ? _maximumDeadlineDelay : remaining,
                _timeProvider);
            using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadlineSource.Token);
            try
            {
                result = await SendCoreAsync(request, remaining, value => stage = value, linkedSource.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
            {
                result = Refuse(request, ProviderFailureKind.Cancellation, "The attempt was cancelled.", stage, exception);
            }
            catch (OperationCanceledException exception)
            {
                result = Refuse(
                    request,
                    ProviderFailureKind.Timeout,
                    "The request did not complete before its deadline.",
                    stage,
                    exception);
            }
        }

        if (result is ProviderEgressRefused { Failure.Kind: ProviderFailureKind.Cancellation } refused
            && !cancellationToken.IsCancellationRequested)
        {
            // A lower boundary reports the attempt's own deadline token as a cancellation; only the caller's token is a
            // caller cancellation, so an expired deadline is always a typed timeout.
            result = Refuse(
                request,
                ProviderFailureKind.Timeout,
                "The request did not complete before its deadline.",
                stage,
                refused.Failure.DiagnosticCause);
        }

        Complete(request, result, activity, started);
        return result;
    }

    private async ValueTask<ProviderEgressResult> SendCoreAsync(
        ProviderEgressRequest request,
        TimeSpan remaining,
        Action<string> enter,
        CancellationToken cancellationToken)
    {
        if (request.Operation is not { } operation)
        {
            return Refuse(
                request,
                ProviderFailureKind.Authorization,
                "No protected operation context was supplied, so provider egress cannot be authorized.",
                "authority");
        }

        if (!TryCreateDestination(request.Message.RequestUri!, out var destination))
        {
            return Refuse(
                request,
                ProviderFailureKind.InvalidRequest,
                "The provider endpoint must be an absolute HTTP(S) address without user information or a fragment.",
                "authority");
        }

        var content = await FreezeBodyAsync(request.Message, cancellationToken).ConfigureAwait(false);
        if (content is not null && content.Body.Length > _options.MaximumRequestBytes)
        {
            return Refuse(
                request,
                ProviderFailureKind.InvalidRequest,
                "The provider request body exceeds the configured request bound.",
                "authority");
        }

        var headers = CreateHeaders(request.Message);
        var method = new NetworkMethod(request.Message.Method.Method);
        var connectTimeout = remaining < _options.ConnectTimeout ? remaining : _options.ConnectTimeout;
        var bounds = new NetworkBounds(
            new NetworkResolutionBounds(connectTimeout),
            new NetworkRequestBounds(connectTimeout, _options.MaximumRequestBytes),
            new NetworkResponseBounds(remaining, _options.MaximumResponseBytes, maximumRedirects: 0));

        var authorization = operation.Authorization;
        var activated = await SelectAuthorityAsync(authorization, cancellationToken).ConfigureAwait(false);
        if (activated is null)
        {
            return Refuse(
                request,
                ProviderFailureKind.Authorization,
                "No security authority was available for the captured authorization context.",
                "authority");
        }

        enter("egress-grant");
        var resources = ProviderEgressBinding.Resources(destination);
        var fingerprint = ProviderEgressBinding.Fingerprint(
            request,
            method,
            destination,
            headers,
            content,
            _options.Classification,
            _options.MaximumResponseBytes);
        var egress = await AuthorizeAsync(
            activated,
            authorization,
            ProviderEgressBinding.Audience,
            resources,
            fingerprint,
            request.Deadline,
            cancellationToken).ConfigureAwait(false);
        if (egress.Refusal is { } egressRefusal)
        {
            return Refuse(request, ProviderFailureKind.Authorization, egressRefusal, "egress-grant", egress.Cause);
        }

        var consumed = await ConsumeAsync(egress.Grant!, resources, fingerprint, cancellationToken).ConfigureAwait(false);
        if (consumed.Refusal is { } consumeRefusal)
        {
            return Refuse(request, ProviderFailureKind.Authorization, consumeRefusal, "egress-grant", consumed.Cause);
        }

        enter("resolution");
        var operationId = _operationIds.Create();
        var resolutionGrant = await AuthorizeAsync(
            activated,
            authorization,
            _resolver.SecurityAudience,
            [NetworkSecurityBinding.ResolutionResource(destination)],
            NetworkSecurityBinding.ResolutionFingerprint(operationId, destination, bounds),
            request.Deadline,
            cancellationToken).ConfigureAwait(false);
        if (resolutionGrant.Refusal is { } resolutionRefusal)
        {
            return Refuse(request, ProviderFailureKind.Authorization, resolutionRefusal, "resolution", resolutionGrant.Cause);
        }

        var resolution = await _resolver.ResolveAsync(
            new NetworkResolutionRequest(operationId, destination, bounds, resolutionGrant.Grant!),
            cancellationToken).ConfigureAwait(false);
        if (resolution is not NetworkResolved resolved)
        {
            return RefuseResolution(request, resolution);
        }

        enter("send");
        var classification = _options.Classification;
        var sendGrant = await AuthorizeAsync(
            activated,
            authorization,
            _transport.SecurityAudience,
            NetworkSecurityBinding.RequestResources(destination, resolved.Addresses),
            NetworkSecurityBinding.RequestFingerprint(
                operationId,
                method,
                destination,
                headers,
                content,
                bounds,
                resolved.Addresses,
                classification),
            request.Deadline,
            cancellationToken).ConfigureAwait(false);
        if (sendGrant.Refusal is { } sendRefusal)
        {
            return Refuse(request, ProviderFailureKind.Authorization, sendRefusal, "send", sendGrant.Cause);
        }

        var send = await _transport.SendAsync(
            new NetworkRequest(
                operationId,
                method,
                destination,
                headers,
                content,
                bounds,
                resolved.Addresses,
                classification,
                sendGrant.Grant!),
            cancellationToken).ConfigureAwait(false);
        return send switch
        {
            NetworkResponseReceived received => new ProviderEgressSent(new ProviderEgressResponse(received.Response)),
            _ => RefuseSend(request, send),
        };
    }

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
        SecurityAuthorizationContext authorization,
        ComponentId audience,
        ImmutableArray<ProtectedResource> resources,
        InputFingerprint fingerprint,
        DateTimeOffset deadline,
        CancellationToken cancellationToken)
    {
        SecurityDecision decision;
        try
        {
            decision = await activation.Authority.AuthorizeAsync(
                new SecurityRequest(
                    _securityRequestIds.Create(),
                    authorization.Scope,
                    toolCallId: null,
                    authorization.Identity,
                    authorization,
                    audience,
                    SecurityOperationKind.Network,
                    SecurityEffect.Egress,
                    resources,
                    fingerprint,
                    deadline),
                hooks: null,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return GrantAttempt.Refused("The security authority was unavailable, so provider egress was not authorized.", exception);
        }

        return decision switch
        {
            SecurityAllowed allowed => GrantAttempt.Allowed(allowed.Grant),
            SecurityApprovalRequired => GrantAttempt.Refused("Provider egress requires an approval that cannot be awaited at this boundary."),
            _ => GrantAttempt.Refused("Provider egress was denied by the security authority."),
        };
    }

    private async ValueTask<GrantAttempt> ConsumeAsync(
        SecurityGrant grant,
        ImmutableArray<ProtectedResource> resources,
        InputFingerprint fingerprint,
        CancellationToken cancellationToken)
    {
        var enforcement = ProviderEgressBinding.Enforcement(grant, resources, fingerprint);
        var intent = new SecurityEnforcementIntent(_intentIds.Create(), null);
        try
        {
            var denial = await SecurityGrantConsumptionHostOperations.ConsumeWithRequiredAuditAsync(
                grant,
                enforcement,
                intent,
                _grantStore,
                _auditDispatcher,
                _auditRecordIds,
                _timeProvider,
                ProviderEgressBinding.IsFreshExact,
                ProviderEgressBinding.DenialMessage,
                cancellationToken).ConfigureAwait(false);
            return denial is null ? GrantAttempt.Allowed(grant) : GrantAttempt.Refused(denial);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return GrantAttempt.Refused("Provider egress enforcement was unavailable, so the grant was not consumed.", exception);
        }
    }

    private static async ValueTask<NetworkRequestContent?> FreezeBodyAsync(
        HttpRequestMessage message,
        CancellationToken cancellationToken)
    {
        if (message.Content is not { } content)
        {
            return null;
        }

        var bytes = await content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        var contentType = content.Headers.ContentType?.ToString() ?? "application/octet-stream";
        return new NetworkRequestContent(contentType, bytes);
    }

    private static NetworkHeaderSet CreateHeaders(HttpRequestMessage message)
    {
        var builder = ImmutableArray.CreateBuilder<NetworkHeader>();
        foreach (var header in message.Headers)
        {
            foreach (var value in header.Value)
            {
                builder.Add(new NetworkHeader(header.Key, value));
            }
        }

        return new NetworkHeaderSet(builder.ToImmutable());
    }

    private static bool TryCreateDestination(Uri uri, out NetworkDestination destination)
    {
        destination = null!;
        if (uri.Scheme is not ("http" or "https")
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

    private ProviderEgressRefused RefuseResolution(ProviderEgressRequest request, NetworkResolutionResult result) => result switch
    {
        NetworkResolutionDenied denied => Refuse(request, ProviderFailureKind.Authorization, denied.SafeMessage, "resolution"),
        NetworkResolutionFailed failed => Refuse(request, MapNetworkFailure(failed.Kind), failed.SafeMessage, "resolution"),
        _ => Refuse(request, ProviderFailureKind.Unavailable, "The resolver returned an unsupported result.", "resolution"),
    };

    private ProviderEgressRefused RefuseSend(ProviderEgressRequest request, NetworkSendResult result) => result switch
    {
        NetworkDenied denied => Refuse(request, ProviderFailureKind.Authorization, denied.SafeMessage, "send"),
        NetworkRequestFailed failed => Refuse(request, MapNetworkFailure(failed.Kind), failed.SafeMessage, "send"),
        NetworkCancelled => Refuse(request, ProviderFailureKind.Cancellation, "The attempt was cancelled.", "send"),
        NetworkRedirectReceived => Refuse(
            request,
            ProviderFailureKind.ProtocolViolation,
            "The provider endpoint answered with a redirect, which provider egress never follows.",
            "send"),
        NetworkRedirectLimitExceeded => Refuse(
            request,
            ProviderFailureKind.ProtocolViolation,
            "The provider endpoint answered with a redirect, which provider egress never follows.",
            "send"),
        NetworkResponseLimitExceeded => Refuse(
            request,
            ProviderFailureKind.ProtocolViolation,
            "The provider response exceeded the configured response bound.",
            "send"),
        _ => Refuse(request, ProviderFailureKind.Unavailable, "The transport returned an unsupported result.", "send"),
    };

    private static ProviderFailureKind MapNetworkFailure(NetworkFailureKind kind) => kind switch
    {
        NetworkFailureKind.Timeout => ProviderFailureKind.Timeout,
        NetworkFailureKind.Cancelled => ProviderFailureKind.Cancellation,
        NetworkFailureKind.UnsupportedScheme => ProviderFailureKind.InvalidRequest,
        NetworkFailureKind.ProtocolViolation => ProviderFailureKind.ProtocolViolation,
        NetworkFailureKind.DnsResolutionFailed or NetworkFailureKind.ConnectionFailed or NetworkFailureKind.TlsFailure
            or NetworkFailureKind.Unknown => ProviderFailureKind.Unavailable,
        _ => ProviderFailureKind.Unavailable,
    };

    private ProviderEgressRefused Refuse(
        ProviderEgressRequest request,
        ProviderFailureKind kind,
        string safeMessage,
        string stage,
        Exception? cause = null)
    {
        if (kind is ProviderFailureKind.Cancellation)
        {
            ProviderEgressLog.Cancelled(_logger, request.ProviderId, request.Kind, stage);
        }
        else
        {
            ProviderEgressLog.Refused(_logger, request.ProviderId, request.Kind, stage, kind);
        }

        return new ProviderEgressRefused(new ProviderFailure(
            kind,
            request.ProviderId,
            requestId: null,
            statusCode: null,
            providerCode: null,
            retryAfter: null,
            safeMessage,
            cause,
            ExtensionData.Empty));
    }

    private void Complete(ProviderEgressRequest request, ProviderEgressResult result, Activity? activity, long started)
    {
        string outcome;
        if (result is ProviderEgressRefused refused)
        {
            outcome = refused.Failure.Kind.ToString().ToLowerInvariant();
            activity.SetFailed(outcome, refused.Failure.Kind.ToString());
        }
        else
        {
            var status = ((ProviderEgressSent) result).Response.StatusCode;
            outcome = "sent";
            ProviderEgressLog.Sent(_logger, request.ProviderId, request.Kind, (int) status);
            activity.SetSuccessful(outcome);
        }

        ProviderMetrics.RecordEgress(OperationTag(request.Kind), outcome, _timeProvider.GetElapsedTime(started));
    }

    private static string OperationTag(ProviderEgressOperation kind) => kind switch
    {
        ProviderEgressOperation.Conversation => "chat",
        ProviderEgressOperation.Embedding => "embedding",
        ProviderEgressOperation.Reranking => "rerank",
        _ => "unknown",
    };

    private sealed record AuthorityActivation(ISecurityAuthority Authority);

    private readonly record struct GrantAttempt(SecurityGrant? Grant, string? Refusal, Exception? Cause)
    {
        internal static GrantAttempt Allowed(SecurityGrant grant) => new(grant, null, null);

        internal static GrantAttempt Refused(string refusal, Exception? cause = null) => new(null, refusal, cause);
    }
}
