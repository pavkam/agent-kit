// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network;

using Microsoft.Extensions.Options;

/// <summary>Performs bounded DNS resolution after consuming exact resolution authority.</summary>
public sealed partial class DefaultNetworkNameResolver: INetworkNameResolver
{
    private readonly ISecurityGrantStore _grantStore;
    private readonly ISecurityAuditDispatcher _auditDispatcher;
    private readonly IIdentifierGenerator<SecurityAuditRecordId> _auditRecordIds;
    private readonly AgentNetworkOptionsSnapshot _options;
    private readonly TimeProvider _timeProvider;
    private readonly IIdentifierGenerator<SecurityEnforcementIntentId> _intentIds;
    private readonly ILogger<DefaultNetworkNameResolver> _logger;

    /// <summary>Initializes the protected system resolver.</summary>
    /// <param name="grantStore">The authoritative grant store.</param>
    /// <param name="auditDispatcher">The required-audit dispatcher consumed at the host boundary.</param>
    /// <param name="timeProvider">The deterministic deadline and freshness clock.</param>
    /// <param name="options">The validated structural policy.</param>
    /// <param name="logger">The optional content-free diagnostic logger; a null value selects a null logger.</param>
    /// <exception cref="ArgumentNullException"><paramref name="grantStore"/>, <paramref name="auditDispatcher"/>, <paramref name="timeProvider"/>, or <paramref name="options"/> is null.</exception>
    public DefaultNetworkNameResolver(
        ISecurityGrantStore grantStore,
        ISecurityAuditDispatcher auditDispatcher,
        TimeProvider timeProvider,
        IOptions<AgentNetworkOptions> options,
        ILogger<DefaultNetworkNameResolver>? logger = null)
        : this(
            grantStore,
            auditDispatcher,
            timeProvider,
            options,
            logger,
            new GuidSecurityEnforcementIntentIdGenerator(),
            new GuidSecurityAuditRecordIdGenerator())
    {
    }

    /// <summary>Initializes the protected system resolver with an injected enforcement-intent identity source.</summary>
    /// <param name="grantStore">The authoritative store that atomically consumes a grant and records permission to start.</param>
    /// <param name="auditDispatcher">The required-audit dispatcher consumed at the host boundary.</param>
    /// <param name="timeProvider">The deterministic deadline and freshness clock.</param>
    /// <param name="options">The validated structural policy.</param>
    /// <param name="logger">The optional content-free diagnostic logger.</param>
    /// <param name="intentIds">The non-null thread-safe source of fresh per-resolution enforcement intent identities.</param>
    /// <param name="auditRecordIds">The audit-record identity generator.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    public DefaultNetworkNameResolver(
        ISecurityGrantStore grantStore,
        ISecurityAuditDispatcher auditDispatcher,
        TimeProvider timeProvider,
        IOptions<AgentNetworkOptions> options,
        ILogger<DefaultNetworkNameResolver>? logger,
        IIdentifierGenerator<SecurityEnforcementIntentId> intentIds,
        IIdentifierGenerator<SecurityAuditRecordId> auditRecordIds)
        : this(
            CreateSnapshot(new NetworkProfileKey("default"), options),
            grantStore,
            auditDispatcher,
            auditRecordIds,
            timeProvider,
            intentIds,
            logger)
    {
    }

    internal DefaultNetworkNameResolver(
        AgentNetworkOptionsSnapshot options,
        ISecurityGrantStore grantStore,
        ISecurityAuditDispatcher auditDispatcher,
        IIdentifierGenerator<SecurityAuditRecordId> auditRecordIds,
        TimeProvider timeProvider,
        IIdentifierGenerator<SecurityEnforcementIntentId> intentIds,
        ILogger<DefaultNetworkNameResolver>? logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(grantStore);
        ArgumentNullException.ThrowIfNull(auditDispatcher);
        ArgumentNullException.ThrowIfNull(auditRecordIds);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(intentIds);
        _options = options;
        _grantStore = grantStore;
        _auditDispatcher = auditDispatcher;
        _auditRecordIds = auditRecordIds;
        _timeProvider = timeProvider;
        _intentIds = intentIds;
        _logger = logger ?? NullLogger<DefaultNetworkNameResolver>.Instance;
    }

    /// <inheritdoc/>
    public ComponentId SecurityAudience { get; } = new("agentkit.network.resolver");

    /// <inheritdoc/>
    private async ValueTask<NetworkResolutionResult> ResolveCoreAsync(
        NetworkResolutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var enforcement = NetworkEnforcementReceipt.Create(
            request.Grant,
            SecurityAudience,
            [NetworkSecurityBinding.ResolutionResource(request.Destination)],
            NetworkSecurityBinding.ResolutionFingerprint(request));
        var intent = new SecurityEnforcementIntent(_intentIds.Create(), null);
        var denial = await NetworkHostGuard.ConsumeWithRequiredAuditAsync(
            request.Grant,
            enforcement,
            intent,
            _grantStore,
            _auditDispatcher,
            _auditRecordIds,
            _timeProvider,
            cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (denial is not null)
        {
            return new NetworkResolutionDenied(denial);
        }

        if (!_options.DestinationPolicy.AllowsSchemeAndHost(request.Destination))
        {
            return new NetworkResolutionDenied("The destination is excluded by the configured network policy.");
        }

        using var timeout = new CancellationTokenSource(request.Bounds.Resolution.ResolutionTimeout, _timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        IPAddress[] addresses;
        try
        {
            addresses = IPAddress.TryParse(request.Destination.Host.Value, out var literal)
                ? [literal]
                : await Dns.GetHostAddressesAsync(request.Destination.Host.Value, linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return new NetworkResolutionFailed(NetworkFailureKind.Timeout, "DNS resolution exceeded its deadline.");
        }
        catch (SocketException)
        {
            return new NetworkResolutionFailed(NetworkFailureKind.DnsResolutionFailed, "DNS resolution failed.");
        }

        var now = _timeProvider.GetUtcNow();
        var expiresAt = now.Add(_options.AddressResolutionLifetime);
        var allowed = addresses
            .Where(_options.DestinationPolicy.AllowsAddress)
            .Select(address => new NetworkAddress(address, now, expiresAt))
            .ToImmutableArray();
        if (allowed.IsEmpty)
        {
            LogNoEligibleAddresses(_logger);
            return new NetworkResolutionDenied("No resolved address is permitted by the configured network policy.");
        }

        return new NetworkResolved(allowed);
    }

    [LoggerMessage(14002, LogLevel.Warning, "Network resolution produced no policy-eligible addresses.")]
    private static partial void LogNoEligibleAddresses(ILogger logger);

    private static AgentNetworkOptionsSnapshot CreateSnapshot(
        NetworkProfileKey profileKey,
        IOptions<AgentNetworkOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var value = options.Value;
        return new AgentNetworkOptionsSnapshot(
            profileKey,
            new NetworkProfileVersion(1),
            value.DestinationPolicy,
            value.AddressResolutionLifetime,
            value.MaximumResponseHeaderKilobytes,
            value.Proxy,
            value.TlsPolicy,
            value.DecompressionPolicy);
    }
}
