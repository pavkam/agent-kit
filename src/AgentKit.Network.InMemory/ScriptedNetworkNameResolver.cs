// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network.InMemory;

/// <summary>
/// A deterministic <see cref="INetworkNameResolver"/> that returns
/// caller-scripted resolution outcomes instead of performing real DNS
/// resolution.
/// </summary>
/// <remarks>
/// Scripted outcomes are keyed by destination. Scripting more than one
/// outcome for a destination returns them in order on successive calls;
/// once only one scripted outcome remains for a destination, every further
/// call for that destination keeps returning it. Every call, including one
/// for an unscripted destination, is recorded to <see cref="Traces"/>
/// before this method returns.
/// </remarks>
public sealed class ScriptedNetworkNameResolver: INetworkNameResolver
{
    private readonly Lock _gate = new();
    private readonly Dictionary<NetworkDestination, Queue<NetworkResolutionResult>> _scripts = [];
    private readonly List<NetworkOperationTrace> _traces = [];
    private readonly TimeProvider _timeProvider;

    /// <summary>Initializes a new instance of the <see cref="ScriptedNetworkNameResolver"/> class.</summary>
    /// <param name="timeProvider">The clock used to timestamp recorded traces.</param>
    /// <exception cref="ArgumentNullException"><paramref name="timeProvider"/> is null.</exception>
    public ScriptedNetworkNameResolver(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
    }

    /// <summary>Gets every resolution attempt this resolver has recorded, in call order.</summary>
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

    /// <summary>Scripts the outcomes this resolver returns for <paramref name="destination"/>.</summary>
    /// <param name="destination">The destination to script.</param>
    /// <param name="results">The outcomes to return, in order, on successive calls for that destination.</param>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="results"/> is empty.</exception>
    public void Script(NetworkDestination destination, params IEnumerable<NetworkResolutionResult> results)
    {
        ArgumentNullException.ThrowIfNull(destination);
        var queue = new Queue<NetworkResolutionResult>(results);
        if (queue.Count == 0)
        {
            throw new ArgumentException("At least one scripted result is required.", nameof(results));
        }

        lock (_gate)
        {
            _scripts[destination] = queue;
        }
    }

    /// <inheritdoc/>
    public ValueTask<NetworkResolutionResult> ResolveAsync(
        NetworkResolutionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        NetworkResolutionResult result;
        lock (_gate)
        {
            _traces.Add(new NetworkOperationTrace(request.Id, request.Destination, "resolve", _timeProvider.GetUtcNow()));

            result = _scripts.TryGetValue(request.Destination, out var queue) && queue.Count > 0
                ? queue.Count > 1 ? queue.Dequeue() : queue.Peek()
                : new NetworkResolutionFailed(
                    NetworkFailureKind.DnsResolutionFailed, $"No scripted resolution for '{request.Destination}'.");
        }

        return ValueTask.FromResult(result);
    }
}
