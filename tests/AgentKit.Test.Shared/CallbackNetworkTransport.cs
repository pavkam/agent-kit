// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

/// <summary>A deterministic transport that consumes its send grant, then answers through a caller callback.</summary>
/// <remarks>
/// The callback decides every outcome, including awaiting cancellation, so a test scripts a denial, a streamed body, a
/// redirect, or a hang without sockets. The transport records each request that reached it after grant consumption.
/// </remarks>
public sealed class CallbackNetworkTransport: INetworkTransport
{
    private readonly Lock _gate = new();
    private readonly ISecurityGrantStore _grantStore;
    private readonly Func<NetworkRequest, CancellationToken, ValueTask<NetworkSendResult>> _callback;
    private readonly List<NetworkRequest> _requests = [];
    private int _intent;

    /// <summary>Initializes the transport.</summary>
    /// <param name="grantStore">The store that consumes send grants.</param>
    /// <param name="callback">Produces the outcome for each request that passed grant consumption.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public CallbackNetworkTransport(
        ISecurityGrantStore grantStore,
        Func<NetworkRequest, CancellationToken, ValueTask<NetworkSendResult>> callback)
    {
        ArgumentNullException.ThrowIfNull(grantStore);
        ArgumentNullException.ThrowIfNull(callback);
        _grantStore = grantStore;
        _callback = callback;
    }

    /// <inheritdoc/>
    public ComponentId SecurityAudience { get; } = new("test.network.transport");

    /// <summary>Gets a snapshot of every request that reached the callback.</summary>
    public IReadOnlyList<NetworkRequest> Requests
    {
        get
        {
            lock (_gate)
            {
                return [.. _requests];
            }
        }
    }

    /// <inheritdoc/>
    public async ValueTask<NetworkSendResult> SendAsync(NetworkRequest request, CancellationToken cancellationToken = default)
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
            NetworkSecurityBinding.RequestResources(request),
            NetworkSecurityBinding.RequestFingerprint(request),
            request.Grant.RevocationVersion);
        var intent = new SecurityEnforcementIntent(
            new SecurityEnforcementIntentId(Guid.Parse($"81000000-0000-0000-0000-{Interlocked.Increment(ref _intent):D12}")),
            null);
        var consumption = await _grantStore
            .ValidateAndConsumeAsync(request.Grant, enforcement, intent, cancellationToken)
            .ConfigureAwait(false);
        if (consumption.Status != GrantConsumptionStatus.Consumed)
        {
            return new NetworkDenied(consumption.SafeMessage);
        }

        lock (_gate)
        {
            _requests.Add(request);
        }

        return await _callback(request, cancellationToken).ConfigureAwait(false);
    }
}
