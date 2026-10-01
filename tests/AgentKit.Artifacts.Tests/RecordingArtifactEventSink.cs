// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Tests;

/// <summary>Records the artifact events it observes, optionally failing or observing delivery order.</summary>
internal sealed class RecordingArtifactEventSink: IArtifactEventSink
{
    /// <summary>Gets the events observed, in delivery order.</summary>
    internal List<ArtifactEvent> Events { get; } = [];

    /// <summary>Gets or sets an exception thrown on delivery after the event is recorded.</summary>
    internal Exception? Failure { get; set; }

    /// <summary>Gets or sets an action invoked on delivery.</summary>
    internal Action? OnPublish { get; set; }

    /// <inheritdoc/>
    public ValueTask PublishAsync(ArtifactEvent artifactEvent, CancellationToken cancellationToken = default)
    {
        Events.Add(artifactEvent);
        OnPublish?.Invoke();
        return Failure is null ? ValueTask.CompletedTask : throw Failure;
    }
}
