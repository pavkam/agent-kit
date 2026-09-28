// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Drains required run-event sinks during engine shutdown within a bounded deadline.</summary>
public interface IRequiredRunEventSinkCoordinator
{
    /// <summary>Waits for required sinks to finish bounded shutdown work.</summary>
    /// <param name="deadline">The maximum time shutdown may spend draining required sinks.</param>
    /// <param name="cancellationToken">Cancels the shutdown wait.</param>
    /// <returns>A task that completes once every required sink drained or the deadline elapsed.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="deadline"/> is negative.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was signalled.</exception>
    public ValueTask DrainAsync(TimeSpan deadline, CancellationToken cancellationToken = default);
}
