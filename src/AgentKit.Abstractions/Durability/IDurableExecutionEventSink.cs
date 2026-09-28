// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Observes immutable durable execution transitions.</summary>
public interface IDurableExecutionEventSink
{
    /// <summary>Publishes one durable execution event.</summary>
    /// <param name="executionEvent">The immutable event.</param>
    /// <param name="cancellationToken">Cancels before publication completes.</param>
    /// <returns>A task that completes when the sink accepts the event.</returns>
    public ValueTask PublishAsync(
        DurableExecutionEvent executionEvent,
        CancellationToken cancellationToken = default);
}
