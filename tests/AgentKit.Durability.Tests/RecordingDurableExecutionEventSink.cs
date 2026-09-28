// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Tests;

/// <summary>A controllable event sink that records what it observed and can be made to fail or block.</summary>
/// <remarks>
/// Instances are shared across a dispatch, so the recorded order is the dispatcher's order. The static
/// <see cref="Log"/> is the only way two distinct sink types can report their relative position, because the
/// dispatcher resolves sinks by implementation type rather than by instance.
/// </remarks>
internal abstract class RecordingDurableExecutionEventSink: IDurableExecutionEventSink
{
    /// <summary>Gets the shared, ordered record of every sink publication attempt.</summary>
    /// <value>A mutable list appended to in dispatch order; a test clears it before acting.</value>
    internal static List<string> Log { get; } = [];

    /// <summary>Gets or sets the exception this sink throws instead of accepting the event.</summary>
    /// <value>Null to accept normally.</value>
    internal Exception? Failure { get; set; }

    /// <summary>Gets or sets a source this sink cancels while the publication is in flight.</summary>
    /// <value>Null to leave the publication token alone.</value>
    internal CancellationTokenSource? CancelWhileRunning { get; set; }

    /// <summary>Gets the events this instance accepted.</summary>
    /// <value>Appended to in acceptance order.</value>
    internal List<DurableExecutionEvent> Accepted { get; } = [];

    /// <inheritdoc/>
    public ValueTask PublishAsync(
        DurableExecutionEvent executionEvent,
        CancellationToken cancellationToken = default)
    {
        Log.Add(GetType().Name);
        CancelWhileRunning?.Cancel();
        cancellationToken.ThrowIfCancellationRequested();
        if (Failure is not null)
        {
            return ValueTask.FromException(Failure);
        }

        Accepted.Add(executionEvent);
        return ValueTask.CompletedTask;
    }
}
