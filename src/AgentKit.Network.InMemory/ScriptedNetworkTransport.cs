// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network.InMemory;

/// <summary>
/// A deterministic <see cref="INetworkTransport"/> that returns
/// caller-scripted send outcomes instead of performing a real network
/// send.
/// </summary>
/// <remarks>
/// When constructed with a <see cref="NetworkDestinationPolicy"/>, requests
/// whose destination the policy rejects are denied before any scripted
/// outcome is consulted and before any trace entry is recorded, mirroring
/// the same structural check the real transport performs before DNS or
/// connection. Scripted outcomes are keyed by method and destination.
/// Scripting more than one outcome for a key returns them in order on
/// successive calls; once only one remains, every further call for that
/// key keeps returning it.
/// </remarks>
public sealed class ScriptedNetworkTransport: INetworkTransport
{
    private readonly Lock _gate = new();
    private readonly Dictionary<(NetworkMethod Method, NetworkDestination Destination), Queue<NetworkSendResult>> _scripts = [];
    private readonly List<NetworkOperationTrace> _traces = [];
    private readonly NetworkDestinationPolicy? _policy;
    private readonly TimeProvider _timeProvider;

    /// <summary>Initializes a new instance of the <see cref="ScriptedNetworkTransport"/> class.</summary>
    /// <param name="timeProvider">The clock used to timestamp recorded traces.</param>
    /// <param name="policy">
    /// The destination policy to enforce before consulting a script, when
    /// one is configured; <see langword="null"/> enforces no policy.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="timeProvider"/> is null.</exception>
    public ScriptedNetworkTransport(TimeProvider timeProvider, NetworkDestinationPolicy? policy = null)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        _timeProvider = timeProvider;
        _policy = policy;
    }

    /// <summary>Gets every send attempt this transport has recorded, in call order.</summary>
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

    /// <summary>Scripts the outcomes this transport returns for <paramref name="method"/> and <paramref name="destination"/>.</summary>
    /// <param name="method">The request method to script.</param>
    /// <param name="destination">The destination to script.</param>
    /// <param name="results">The outcomes to return, in order, on successive calls for that key.</param>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="results"/> is empty.</exception>
    public void Script(NetworkMethod method, NetworkDestination destination, params IEnumerable<NetworkSendResult> results)
    {
        ArgumentNullException.ThrowIfNull(destination);
        var queue = new Queue<NetworkSendResult>(results);
        if (queue.Count == 0)
        {
            throw new ArgumentException("At least one scripted result is required.", nameof(results));
        }

        lock (_gate)
        {
            _scripts[(method, destination)] = queue;
        }
    }

    /// <inheritdoc/>
    public ValueTask<NetworkSendResult> SendAsync(NetworkRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (_policy is not null && !_policy.AllowsSchemeAndHost(request.Destination))
        {
            return ValueTask.FromResult<NetworkSendResult>(
                new NetworkDenied($"Destination '{request.Destination}' is not permitted by the configured policy."));
        }

        NetworkSendResult result;
        lock (_gate)
        {
            _traces.Add(new NetworkOperationTrace(request.Id, request.Destination, "send", _timeProvider.GetUtcNow()));

            var key = (request.Method, request.Destination);
            result = _scripts.TryGetValue(key, out var queue) && queue.Count > 0
                ? queue.Count > 1 ? queue.Dequeue() : queue.Peek()
                : new NetworkRequestFailed(
                    NetworkFailureKind.Unknown,
                    $"No scripted result for '{request.Method} {request.Destination}'.",
                    sideEffectCertain: true);
        }

        return ValueTask.FromResult(result);
    }
}
