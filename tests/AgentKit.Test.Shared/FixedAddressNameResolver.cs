// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

using System.Net;

/// <summary>A deterministic resolver that consumes its resolution grant and answers every host with one fixed address.</summary>
/// <remarks>It performs no DNS and exposes the destinations it resolved so a test can prove ordering and absence.</remarks>
public sealed class FixedAddressNameResolver: INetworkNameResolver
{
    private readonly Lock _gate = new();
    private readonly ISecurityGrantStore _grantStore;
    private readonly TimeProvider _timeProvider;
    private readonly List<NetworkDestination> _resolved = [];
    private int _intent;

    /// <summary>Initializes the resolver over a grant store and clock.</summary>
    /// <param name="grantStore">The store that consumes resolution grants.</param>
    /// <param name="timeProvider">The clock stamping address lifetimes.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public FixedAddressNameResolver(ISecurityGrantStore grantStore, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(grantStore);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _grantStore = grantStore;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc/>
    public ComponentId SecurityAudience { get; } = new("test.network.resolver");

    /// <summary>Gets the destinations resolved so far, in order.</summary>
    public IReadOnlyList<NetworkDestination> Resolved
    {
        get
        {
            lock (_gate)
            {
                return [.. _resolved];
            }
        }
    }

    /// <inheritdoc/>
    public async ValueTask<NetworkResolutionResult> ResolveAsync(
        NetworkResolutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var enforcement = new SecurityEnforcementRequest(
            request.Grant.Scope,
            request.Grant.Identity,
            request.Grant.Authorization,
            SecurityAudience,
            SecurityOperationKind.Network,
            SecurityEffect.Egress,
            [NetworkSecurityBinding.ResolutionResource(request.Destination)],
            NetworkSecurityBinding.ResolutionFingerprint(request),
            request.Grant.RevocationVersion);
        var intent = new SecurityEnforcementIntent(
            new SecurityEnforcementIntentId(Guid.Parse($"70000000-0000-0000-0000-{Interlocked.Increment(ref _intent):D12}")),
            null);
        var consumption = await _grantStore
            .ValidateAndConsumeAsync(request.Grant, enforcement, intent, cancellationToken)
            .ConfigureAwait(false);
        if (consumption.Status != GrantConsumptionStatus.Consumed)
        {
            return new NetworkResolutionDenied(consumption.SafeMessage);
        }

        lock (_gate)
        {
            _resolved.Add(request.Destination);
        }

        var now = _timeProvider.GetUtcNow();
        return new NetworkResolved([new NetworkAddress(IPAddress.Parse("93.184.216.34"), now, now.AddHours(1))]);
    }
}
