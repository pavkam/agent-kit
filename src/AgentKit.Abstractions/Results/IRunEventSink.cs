// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Receives one run's published events for delivery to one additive observation destination.</summary>
/// <remarks>
/// A sink neither mints domain events nor feeds a decision back into the run: the component that owns a
/// domain transition owns creation of the <see cref="RunEvent"/> it publishes. Sinks are registered additively
/// through <see cref="RunEventSinkRegistration"/>, which declares each sink's stable identity, delivery
/// requirement, and position in the deterministic fan-out order; the selected <see cref="IOutputPublisher"/>
/// consumes every registered sink rather than a competing observer abstraction. Stateless sinks are normally
/// thread-safe singletons since one engine can run many concurrent run scopes; a required sink whose delivery
/// must complete before settlement may instead be scoped. Direct implementation is the extension path.
/// </remarks>
public interface IRunEventSink
{
    /// <summary>Delivers one published event to this sink.</summary>
    /// <param name="runEvent">The nonnull event, whose <c>(RunId, Sequence)</c> composite is its stable identity.</param>
    /// <param name="cancellationToken">
    /// Cancels this sink's own delivery wait. A <see cref="RunEventDelivery.Required"/> sink's cancellation
    /// propagates to the publisher's acceptance boundary; a <see cref="RunEventDelivery.BestEffort"/> sink's
    /// cancellation or failure is isolated and never propagates to the run.
    /// </param>
    /// <returns>Completion of this sink's own delivery, which may be durable acceptance rather than external acknowledgement.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="runEvent"/> is null.</exception>
    /// <exception cref="OperationCanceledException">This sink's delivery wait is cancelled.</exception>
    public ValueTask PublishAsync(RunEvent runEvent, CancellationToken cancellationToken = default);
}
