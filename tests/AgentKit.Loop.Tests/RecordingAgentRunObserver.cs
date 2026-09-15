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

    /// <inheritdoc/>
    public ValueTask OnEventAsync(AgentRunEvent runEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(runEvent);
        Events.Add(runEvent);
        EventRecorded?.Invoke(runEvent);
        return ThrowAfterRecording
            ? ValueTask.FromException(new InvalidOperationException("observer failure"))
            : ValueTask.CompletedTask;
    }
}
