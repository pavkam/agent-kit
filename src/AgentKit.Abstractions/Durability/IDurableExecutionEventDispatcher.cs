// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Activates and fans out durable execution events to registered sinks.</summary>
public interface IDurableExecutionEventDispatcher
{
    /// <summary>Publishes one event through sinks selected for the captured context.</summary>
    /// <param name="context">The captured durability composition.</param>
    /// <param name="executionEvent">The immutable event.</param>
    /// <param name="cancellationToken">Cancels before dispatch completes.</param>
    /// <returns>A task that completes when required sinks have been invoked.</returns>
    public ValueTask PublishAsync(
        DurableExecutionContext context,
        DurableExecutionEvent executionEvent,
        CancellationToken cancellationToken = default);
}
