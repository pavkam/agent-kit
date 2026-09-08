// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network.InMemory;

/// <summary>Returns deterministic resolution scenarios after exact grant consumption.</summary>
public sealed partial class ScriptedNetworkNameResolver: INetworkNameResolver
{
    private readonly Lock _gate = new();
    private readonly Dictionary<NetworkDestination, Queue<NetworkResolutionResult>> _scripts = [];
    private readonly List<NetworkOperationTrace> _traces = [];
    private readonly ISecurityGrantStore _grantStore;
    private readonly TimeProvider _timeProvider;
    private readonly IIdentifierGenerator<SecurityEnforcementIntentId> _intentIds;
    private readonly ILogger<ScriptedNetworkNameResolver> _logger;

    /// <summary>Initializes the protected deterministic resolver.</summary>
    /// <param name="grantStore">The authoritative grant store.</param>
    /// <param name="timeProvider">The deterministic trace clock.</param>
    /// <param name="logger">The optional content-free diagnostic logger.</param>
    /// <exception cref="ArgumentNullException"><paramref name="grantStore"/> or <paramref name="timeProvider"/> is null.</exception>
    public ScriptedNetworkNameResolver(
        ISecurityGrantStore grantStore,
        TimeProvider timeProvider,
        ILogger<ScriptedNetworkNameResolver>? logger = null)
        : this(grantStore, timeProvider, logger, new GuidSecurityEnforcementIntentIdGenerator())
    {
    }

    /// <summary>Initializes the protected deterministic resolver with an injected enforcement-intent identity source.</summary>
    /// <param name="grantStore">The authoritative store that atomically consumes a grant and records permission to start.</param>
    /// <param name="timeProvider">The deterministic trace clock.</param>
    /// <param name="logger">The optional content-free diagnostic logger.</param>
    /// <param name="intentIds">The non-null thread-safe source of fresh per-resolution enforcement intent identities.</param>
    /// <exception cref="ArgumentNullException"><paramref name="grantStore"/>, <paramref name="timeProvider"/>, or <paramref name="intentIds"/> is null.</exception>
    public ScriptedNetworkNameResolver(
        ISecurityGrantStore grantStore,
        TimeProvider timeProvider,
        ILogger<ScriptedNetworkNameResolver>? logger,
        IIdentifierGenerator<SecurityEnforcementIntentId> intentIds)
    {
        ArgumentNullException.ThrowIfNull(grantStore);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(intentIds);
        _grantStore = grantStore;
        _timeProvider = timeProvider;
        _intentIds = intentIds;
        _logger = logger ?? NullLogger<ScriptedNetworkNameResolver>.Instance;
    }

    /// <inheritdoc/>
    public ComponentId SecurityAudience { get; } = new("agentkit.network.in-memory.resolver");

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

    /// <summary>Scripts one or more resolution outcomes for a destination.</summary>
    /// <param name="destination">The exact destination key.</param>
    /// <param name="results">The non-empty ordered outcomes.</param>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> or <paramref name="results"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="results"/> is empty.</exception>
    public void Script(NetworkDestination destination, params IEnumerable<NetworkResolutionResult> results)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(results);
        var queue = new Queue<NetworkResolutionResult>(results);
        ArgumentOutOfRangeException.ThrowIfZero(queue.Count, nameof(results));
        lock (_gate)
        {
            _scripts[destination] = queue;
        }
    }

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

        lock (_gate)
        {
            _traces.Add(new NetworkOperationTrace(request.Id, request.Destination, "resolve", _timeProvider.GetUtcNow()));
            return _scripts.TryGetValue(request.Destination, out var queue) && queue.Count > 0
                ? queue.Count == 1 ? queue.Peek() : queue.Dequeue()
                : new NetworkResolutionFailed(NetworkFailureKind.DnsResolutionFailed, "No scripted resolution is configured.");
        }
    }
}
