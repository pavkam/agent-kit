// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network.InMemory;

/// <summary>
/// A redacted record that one phase of one network operation was reached,
/// with no header, body, or credential content.
/// </summary>
/// <remarks>
/// Tests use the absence of a trace entry to prove that a denied
/// operation's destination was never resolved, connected, or transmitted.
/// </remarks>
public sealed record NetworkOperationTrace
{
    /// <summary>Initializes a new instance of the <see cref="NetworkOperationTrace"/> record.</summary>
    /// <param name="id">The operation this trace entry belongs to.</param>
    /// <param name="destination">The destination this phase concerned.</param>
    /// <param name="phase">The phase reached, such as <c>"resolve"</c> or <c>"send"</c>.</param>
    /// <param name="at">The instant this phase was reached.</param>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="phase"/> is null, empty, or consists only of whitespace.
    /// </exception>
    public NetworkOperationTrace(NetworkOperationId id, NetworkDestination destination, string phase, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentException.ThrowIfNullOrWhiteSpace(phase);

        Id = id;
        Destination = destination;
        Phase = phase;
        At = at;
    }

    /// <summary>Gets the operation this trace entry belongs to.</summary>
    public NetworkOperationId Id { get; init; }

    /// <summary>Gets the destination this phase concerned.</summary>
    public NetworkDestination Destination { get; init; }

    /// <summary>Gets the phase reached, such as <c>"resolve"</c> or <c>"send"</c>.</summary>
    public string Phase { get; init; }

    /// <summary>Gets the instant this phase was reached.</summary>
    public DateTimeOffset At { get; init; }
}
