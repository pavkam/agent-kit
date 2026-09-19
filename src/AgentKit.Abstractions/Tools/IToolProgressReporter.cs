// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports a bounded, invocation-scoped, live-only progress update from one running <see cref="IToolInvoker"/> attempt.</summary>
/// <remarks>
/// This is a deliberately reduced interim shape covering only the live-update surface. It intentionally omits the
/// durable semantic-checkpoint API described by the tool-call lifecycle contract (bounded complete snapshots with
/// declared cadence, replacement behavior, and post-outcome fencing); that surface lands with the executor pipeline
/// that can enforce cadence and staleness. Progress is diagnostic only: it never becomes message content, never
/// authorizes an effect, and reaching outcome staging fences and ignores any further report.
/// </remarks>
public interface IToolProgressReporter
{
    /// <summary>Publishes one bounded live-only progress message for the current invocation attempt.</summary>
    /// <param name="message">The bounded, safe, human-readable progress text.</param>
    /// <param name="cancellationToken">Propagates cancellation of the reporting call itself.</param>
    /// <returns>A task that completes once the update has been accepted for live fan-out.</returns>
    /// <exception cref="ArgumentException"><paramref name="message"/> is null, empty, or whitespace.</exception>
    public ValueTask ReportAsync(string message, CancellationToken cancellationToken = default);
}
