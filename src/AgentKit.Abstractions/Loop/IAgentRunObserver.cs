// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Receives ordered, provisional progress from one agent-loop run.</summary>
/// <remarks>
/// Observer delivery is best effort and does not participate in run control, persistence, or tool execution.
/// Implementations should return promptly. An observer failure is isolated by the loop and cannot fail the run
/// or cause a model or tool effect to be repeated.
/// </remarks>
public interface IAgentRunObserver
{
    /// <summary>Observes the next item of provisional run progress.</summary>
    /// <param name="runEvent">The immutable progress item.</param>
    /// <param name="cancellationToken">Cancels this bounded delivery when the run itself is cancelled.</param>
    /// <returns>An operation that completes when this event has been consumed or intentionally dropped.</returns>
    public ValueTask OnEventAsync(AgentRunEvent runEvent, CancellationToken cancellationToken = default);
}
