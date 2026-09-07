// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Anthropic.Tests.Fakes;

/// <summary>
/// An <see cref="IModelResponseObserver"/> test double that records every
/// delivered event in order, for assertion.
/// </summary>
internal sealed class RecordingModelResponseObserver: IModelResponseObserver
{
    private readonly List<ModelResponseEvent> _events = [];

    /// <summary>Gets the events delivered so far, in delivery order.</summary>
    public IReadOnlyList<ModelResponseEvent> Events => _events;

    /// <inheritdoc/>
    public ValueTask OnEventAsync(ModelResponseEvent responseEvent, CancellationToken cancellationToken = default)
    {
        _events.Add(responseEvent);
        return ValueTask.CompletedTask;
    }
}
