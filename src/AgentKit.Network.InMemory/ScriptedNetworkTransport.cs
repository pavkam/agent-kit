// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network.InMemory;

/// <summary>Sends deterministic network scenarios only after exact egress-grant consumption.</summary>
public sealed partial class ScriptedNetworkTransport: INetworkTransport
{
    private readonly Lock _gate = new();
    private readonly Dictionary<NetworkDestination, Queue<NetworkSendResult>> _scripts = [];
    private readonly List<NetworkOperationTrace> _traces = [];
    private readonly ISecurityGrantStore _grantStore;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ScriptedNetworkTransport> _logger;

    /// <summary>Initializes the protected deterministic transport.</summary>
    /// <param name="grantStore">The authoritative single-use grant store.</param>
    /// <param name="timeProvider">The deterministic trace clock.</param>
    /// <param name="logger">The optional content-free diagnostic logger.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    public ScriptedNetworkTransport(
        ISecurityGrantStore grantStore,
        TimeProvider timeProvider,
        ILogger<ScriptedNetworkTransport>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(grantStore);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _grantStore = grantStore;
        _timeProvider = timeProvider;
        _logger = logger ?? NullLogger<ScriptedNetworkTransport>.Instance;
    }

    /// <inheritdoc/>
    public ComponentId SecurityAudience { get; } = new("agentkit.network.in-memory.transport");

    /// <summary>Gets a stable snapshot of redacted admitted operations.</summary>
    public IReadOnlyList<NetworkOperationTrace> Traces
    {
        get
        {
            lock (_gate)
            {
                return [.. _traces];
            }
        }
    }

    /// <summary>Scripts one or more send outcomes for an exact destination.</summary>
    /// <param name="destination">The exact destination key.</param>
    /// <param name="results">The non-empty ordered terminal outcomes.</param>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> or <paramref name="results"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="results"/> is empty.</exception>
    public void Script(NetworkDestination destination, params IEnumerable<NetworkSendResult> results)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(results);
        var queue = new Queue<NetworkSendResult>(results);
        ArgumentOutOfRangeException.ThrowIfZero(queue.Count, nameof(results));
        lock (_gate)
        {
            _scripts[destination] = queue;
        }
    }

    /// <inheritdoc/>
    private async ValueTask<NetworkSendResult> SendCoreAsync(
        NetworkRequest request,
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
                NetworkSecurityBinding.RequestResources(request),
                NetworkSecurityBinding.RequestFingerprint(request),
                request.Grant.RevocationVersion),
            cancellationToken).ConfigureAwait(false);
        if (consumption.Status != GrantConsumptionStatus.Consumed)
        {
            return new NetworkDenied(consumption.SafeMessage);
        }

        lock (_gate)
        {
            _traces.Add(new NetworkOperationTrace(request.Id, request.Destination, "send", _timeProvider.GetUtcNow()));
            return _scripts.TryGetValue(request.Destination, out var queue) && queue.Count > 0
                ? queue.Dequeue()
                : new NetworkRequestFailed(
                    NetworkFailureKind.ConnectionFailed,
                    "No scripted send is configured.",
                    sideEffectCertain: true);
        }
    }
}
