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
    private readonly ILogger<DefaultNetworkNameResolver> _logger;

    /// <summary>Initializes the protected system resolver.</summary>
    /// <param name="grantStore">The authoritative grant store.</param>
    /// <param name="timeProvider">The deterministic deadline and freshness clock.</param>
    /// <param name="options">The validated structural policy.</param>
    /// <param name="logger">The content-free diagnostic logger.</param>
    /// <exception cref="ArgumentNullException">Any dependency is null.</exception>
    public DefaultNetworkNameResolver(
        ISecurityGrantStore grantStore,
        TimeProvider timeProvider,
        IOptions<AgentNetworkOptions> options,
        ILogger<DefaultNetworkNameResolver>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(grantStore);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        _grantStore = grantStore;
        _timeProvider = timeProvider;
        _policy = options.Value.DestinationPolicy;
        _addressLifetime = options.Value.AddressResolutionLifetime;
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
        var consumption = await _grantStore.ValidateAndConsumeAsync(
            request.Grant,
            new SecurityEnforcementRequest(
                request.Grant.Scope,
                request.Grant.Identity,
                SecurityAudience,
                SecurityOperationKind.Network,
                SecurityEffect.Egress,
                [NetworkSecurityBinding.ResolutionResource(request.Destination)],
                NetworkSecurityBinding.ResolutionFingerprint(request),
                request.Grant.RevocationVersion),
            cancellationToken).ConfigureAwait(false);
        if (consumption.Status != GrantConsumptionStatus.Consumed)
        {
            return new NetworkResolutionDenied(consumption.SafeMessage);
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
