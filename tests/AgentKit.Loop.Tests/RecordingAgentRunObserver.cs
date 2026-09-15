// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop.Tests;

/// <summary>Records run events and optionally rejects delivery after recording it.</summary>
internal sealed class RecordingAgentRunObserver: IAgentRunObserver
{
    /// <summary>Gets the events observed in delivery order.</summary>
    internal List<AgentRunEvent> Events { get; } = [];

    /// <summary>Gets or sets whether every delivery throws after it is recorded.</summary>
    internal bool ThrowAfterRecording { get; set; }

    /// <summary>Gets or sets an optional action invoked after each event is recorded.</summary>
    internal Action<AgentRunEvent>? EventRecorded { get; set; }

    /// <summary>
    /// Gets or sets a predicate selecting events whose delivery never completes on its own: the observer awaits
    /// the supplied token instead, mirroring a stalled sink. <see cref="DeliveryStalled"/> is signalled once such a
    /// delivery is actually waiting.
    /// </summary>
    internal Func<AgentRunEvent, bool>? StallDelivery { get; set; }

    /// <summary>Gets a completion signalled when a stalled delivery is waiting on its token.</summary>
    internal TaskCompletionSource DeliveryStalled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <inheritdoc/>
    public async ValueTask OnEventAsync(AgentRunEvent runEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(runEvent);
        Events.Add(runEvent);
        EventRecorded?.Invoke(runEvent);
        if (StallDelivery?.Invoke(runEvent) == true)
        {
            _ = DeliveryStalled.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }

        if (ThrowAfterRecording)
        {
            throw new InvalidOperationException("observer failure");
        }
    }
}
