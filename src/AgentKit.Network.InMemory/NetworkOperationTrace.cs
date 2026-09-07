// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network.InMemory;

/// <summary>Records a content-free deterministic network phase for effect-order assertions.</summary>
public sealed record NetworkOperationTrace
{
    /// <summary>Initializes one redacted trace.</summary>
    /// <param name="id">The operation identity.</param>
    /// <param name="destination">The canonical destination.</param>
    /// <param name="phase">The non-empty phase name.</param>
    /// <param name="at">The injected-clock timestamp.</param>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="phase"/> is blank.</exception>
    public NetworkOperationTrace(
        NetworkOperationId id,
        NetworkDestination destination,
        string phase,
        DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentException.ThrowIfNullOrWhiteSpace(phase);
        Id = id;
        Destination = destination;
        Phase = phase;
        At = at;
    }

    /// <summary>Gets the operation identity.</summary>
    public NetworkOperationId Id { get; }
    /// <summary>Gets the canonical destination without headers or body.</summary>
    public NetworkDestination Destination { get; }
    /// <summary>Gets the reached phase.</summary>
    public string Phase { get; }
    /// <summary>Gets the injected-clock timestamp.</summary>
    public DateTimeOffset At { get; }
}
