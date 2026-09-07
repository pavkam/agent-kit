// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network;

using Microsoft.Extensions.Options;

/// <summary>The default, real <see cref="INetworkNameResolver"/>: bounded system DNS resolution filtered by a configured destination policy.</summary>
/// <remarks>
/// See <see cref="NetworkDestinationPolicy"/> for the reduced-scope
/// rationale shared by every implementation of this contract. An IP
/// literal host is validated directly without a DNS lookup. Every
/// candidate address the system resolver returns is filtered by the
/// configured policy; an unresolved or fully filtered destination never
/// reaches <see cref="DefaultNetworkTransport"/>.
/// </remarks>
public sealed class DefaultNetworkNameResolver: INetworkNameResolver
{
    private readonly NetworkDestinationPolicy _policy;
    private readonly TimeSpan _addressLifetime;
    private readonly TimeProvider _timeProvider;

    /// <summary>Initializes a new instance of the <see cref="DefaultNetworkNameResolver"/> class.</summary>
    /// <param name="timeProvider">The clock used to timestamp resolved addresses.</param>
    /// <param name="options">The validated resolver options.</param>
    /// <exception cref="ArgumentNullException"><paramref name="timeProvider"/> or <paramref name="options"/> is null.</exception>
    public DefaultNetworkNameResolver(TimeProvider timeProvider, IOptions<AgentNetworkOptions> options)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);

        _timeProvider = timeProvider;
        _policy = options.Value.DestinationPolicy;
        _addressLifetime = options.Value.AddressResolutionLifetime;
    }

    /// <inheritdoc/>
    public async ValueTask<NetworkResolutionResult> ResolveAsync(
        NetworkResolutionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_policy.AllowsSchemeAndHost(request.Destination))
        {
            return new NetworkResolutionDenied(
                $"Destination '{request.Destination}' is not permitted by the configured policy.");
        }

        using var timeoutSource = new CancellationTokenSource(request.Bounds.ConnectTimeout, _timeProvider);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);

        IPAddress[] addresses;
        try
        {
            addresses = IPAddress.TryParse(request.Destination.Host.Value, out var literal)
                ? [literal]
                : await Dns.GetHostAddressesAsync(request.Destination.Host.Value, linkedSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return new NetworkResolutionFailed(NetworkFailureKind.Timeout, "DNS resolution did not complete before its deadline.");
        }
        catch (SocketException)
        {
            return new NetworkResolutionFailed(NetworkFailureKind.DnsResolutionFailed, "DNS resolution failed.");
        }

        if (addresses.Length == 0)
        {
            return new NetworkResolutionFailed(NetworkFailureKind.DnsResolutionFailed, "DNS resolution returned no addresses.");
        }

        var now = _timeProvider.GetUtcNow();
        var expiresAt = now + _addressLifetime;
        var allowed = addresses
            .Where(_policy.AllowsAddress)
            .Select(address => new NetworkAddress(address, now, expiresAt))
            .ToImmutableArray();

        return allowed.IsEmpty
            ? new NetworkResolutionDenied(
                $"Every address resolved for '{request.Destination}' is excluded by the configured policy.")
            : new NetworkResolved(allowed);
    }
}
