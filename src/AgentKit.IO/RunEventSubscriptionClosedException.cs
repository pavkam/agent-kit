// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO;

/// <summary>Reports explicit delivery loss when a slow consumer or premature hub disposal terminates enumeration.</summary>
internal sealed class RunEventSubscriptionClosedException: InvalidOperationException
{
    /// <summary>Creates a structural delivery failure with an exact first unavailable sequence.</summary>
    /// <param name="state">Either slow-consumer disconnection or premature hub disposal.</param>
    /// <param name="firstUnavailableSequence">The first queued or rejected event discarded for this subscriber, or null when no event was pending.</param>
    /// <exception cref="ArgumentException"><paramref name="state"/> is not a delivery-failure state.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A present <paramref name="firstUnavailableSequence"/> is not positive.</exception>
    internal RunEventSubscriptionClosedException(RunEventSubscriptionState state, long? firstUnavailableSequence)
       : base("The run-event subscription ended before delivery completed.")
    {
        ArgumentException.ThrowIfNotEqual(state is RunEventSubscriptionState.SlowConsumer or RunEventSubscriptionState.HubDisposed, true, nameof(state));
        if (firstUnavailableSequence is { } sequence)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(sequence, 1, nameof(firstUnavailableSequence));
        }
        State = state;
        FirstUnavailableSequence = firstUnavailableSequence;
    }

    /// <summary>Gets the explicit abnormal delivery outcome.</summary>
    /// <value>Slow-consumer disconnection or premature hub disposal, never a semantic run outcome.</value>
    internal RunEventSubscriptionState State { get; }

    /// <summary>Gets the earliest event sequence this subscriber can no longer read.</summary>
    /// <value>A positive sequence when a pending or rejected event was discarded; otherwise null.</value>
    internal long? FirstUnavailableSequence { get; }
}
