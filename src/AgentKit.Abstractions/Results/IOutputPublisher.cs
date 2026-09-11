// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Owns run-event publication and exposure of the already settled-or-recovery-required final envelope.</summary>
/// <remarks>The selected run-scoped publisher owns sequence allocation, durable range reservations, required sink policy and bounded fan-out. It never owns semantic run transitions. Direct implementations must preserve durable identities and distinguish required delivery from best-effort subscribers.</remarks>
public interface IOutputPublisher
{
    /// <summary>Accepts one immutable, correlated event through the selected publication and sink policy.</summary>
    /// <param name="runEvent">The nonnull event using a sequence allocated by this run's publisher.</param>
    /// <param name="cancellationToken">Cancels publication waiting according to the acceptance boundary; it does not undo committed acceptance.</param>
    /// <returns>Completion of the configured acceptance boundary, which may be durable intent acceptance rather than external acknowledgement.</returns>
    /// <exception cref="ArgumentNullException">The event is null.</exception>
    /// <exception cref="ArgumentException">Event correlation or sequence violates the captured publication contract.</exception>
    /// <exception cref="OperationCanceledException">Publication waiting is cancelled.</exception>
    public ValueTask PublishAsync(RunEvent runEvent, CancellationToken cancellationToken = default);

    /// <summary>Exposes the final envelope after the operation owner completes its bounded settlement attempt.</summary>
    /// <typeparam name="TOutput">The validated immutable output type selected for the run.</typeparam>
    /// <param name="result">The nonnull final envelope matching this run and output type.</param>
    /// <param name="cancellationToken">Cancels the caller's publication wait before acceptance; it never changes the envelope's settlement status.</param>
    /// <returns>Completion of final-result exposure to subscribers; repeated equivalent publication is idempotent.</returns>
    /// <exception cref="ArgumentNullException">The result is null.</exception>
    /// <exception cref="ArgumentException">The result addresses another run or output type.</exception>
    /// <exception cref="InvalidOperationException">A conflicting final result was already published.</exception>
    /// <exception cref="OperationCanceledException">The caller's wait is cancelled before acceptance.</exception>
    /// <remarks>This operation adds no required effect that retroactively determines settlement. Required result persistence and publication intent are prepared by the settlement protocol before the envelope becomes observable.</remarks>
    public ValueTask CompleteAsync<TOutput>(AgentRunFinished<TOutput> result, CancellationToken cancellationToken = default);
}
