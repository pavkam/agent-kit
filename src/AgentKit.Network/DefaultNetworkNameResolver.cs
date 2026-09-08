// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network;

using Microsoft.Extensions.Options;

/// <summary>Performs bounded DNS resolution after consuming exact resolution authority.</summary>
public sealed partial class DefaultNetworkNameResolver: INetworkNameResolver
{
    private readonly ISecurityGrantStore _grantStore;
    private readonly NetworkDestinationPolicy _policy;
    private readonly TimeSpan _addressLifetime;
    private readonly TimeProvider _timeProvider;
    private readonly IIdentifierGenerator<SecurityEnforcementIntentId> _intentIds;
    private readonly ILogger<DefaultNetworkNameResolver> _logger;

    /// <summary>Initializes the protected system resolver.</summary>
    /// <param name="grantStore">The authoritative grant store.</param>
    /// <param name="timeProvider">The deterministic deadline and freshness clock.</param>
    /// <param name="options">The validated structural policy.</param>
    /// <param name="logger">The optional content-free diagnostic logger; a null value selects a null logger.</param>
    /// <exception cref="ArgumentNullException"><paramref name="grantStore"/>, <paramref name="timeProvider"/>, or <paramref name="options"/> is null.</exception>
    public DefaultNetworkNameResolver(
        ISecurityGrantStore grantStore,
        TimeProvider timeProvider,
        IOptions<AgentNetworkOptions> options,
        ILogger<DefaultNetworkNameResolver>? logger = null)
        : this(grantStore, timeProvider, options, logger, new GuidSecurityEnforcementIntentIdGenerator())
    {
    }

    /// <summary>Initializes the protected system resolver with an injected enforcement-intent identity source.</summary>
    /// <param name="grantStore">The authoritative store that atomically consumes a grant and records permission to start.</param>
    /// <param name="timeProvider">The deterministic deadline and freshness clock.</param>
    /// <param name="options">The validated structural policy.</param>
    /// <param name="logger">The optional content-free diagnostic logger.</param>
    /// <param name="intentIds">The non-null thread-safe source of fresh per-resolution enforcement intent identities.</param>
    /// <exception cref="ArgumentNullException"><paramref name="grantStore"/>, <paramref name="timeProvider"/>, <paramref name="options"/>, or <paramref name="intentIds"/> is null.</exception>
    public DefaultNetworkNameResolver(
        ISecurityGrantStore grantStore,
        TimeProvider timeProvider,
        IOptions<AgentNetworkOptions> options,
        ILogger<DefaultNetworkNameResolver>? logger,
        IIdentifierGenerator<SecurityEnforcementIntentId> intentIds)
    {
        ArgumentNullException.ThrowIfNull(grantStore);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(intentIds);
        _grantStore = grantStore;
        _timeProvider = timeProvider;
        _policy = options.Value.DestinationPolicy;
        _addressLifetime = options.Value.AddressResolutionLifetime;
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
        var consumption = await _grantStore.ValidateAndConsumeAsync(
            request.Grant,
            enforcement,
            intent,
            cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (!NetworkEnforcementReceipt.IsFreshExact(consumption, request.Grant, enforcement, intent))
        {
            return new NetworkResolutionDenied(consumption.Status == GrantConsumptionStatus.Consumed
                ? "The grant store did not retain a fresh exact enforcement-intent receipt."
                : consumption.SafeMessage);
        }

        if (!_policy.AllowsSchemeAndHost(request.Destination))
        {
            return new NetworkResolutionDenied("The destination is excluded by the configured network policy.");
        }

        using var timeout = new CancellationTokenSource(request.Bounds.ConnectTimeout, _timeProvider);
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
        var expiresAt = now.Add(_addressLifetime);
        var allowed = addresses
            .Where(_policy.AllowsAddress)
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
}
