// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Observes immutable tool-runtime activity without influencing any call's outcome.</summary>
/// <remarks>
/// <para>
/// Sinks are additive and observational. The executor publishes after the corresponding fact is established and
/// isolates every sink failure: a sink that throws, hangs past cancellation, or is unavailable never changes a result,
/// a recording, a retry decision, or another sink's delivery. Events contain identities, tool identity, status and
/// certainty only; arguments, results, paths, and exception text never appear.
/// </para>
/// <para>Implementations must be safe to call concurrently and must not mutate shared state the runtime reads.</para>
/// </remarks>
public interface IToolEventSink
{
    /// <summary>Observes one immutable tool event.</summary>
    /// <param name="toolEvent">The nonnull event to observe.</param>
    /// <param name="cancellationToken">Cancels delivery; the executor treats cancellation of the batch as propagating.</param>
    /// <returns>A task that completes when the sink has observed the event.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="toolEvent"/> is null.</exception>
    public ValueTask PublishAsync(ToolEvent toolEvent, CancellationToken cancellationToken = default);
}
