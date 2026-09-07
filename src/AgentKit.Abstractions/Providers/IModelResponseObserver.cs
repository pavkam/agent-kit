// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Receives the ordered <see cref="ModelResponseEvent"/> sequence produced
/// by one <see cref="ILlmModel"/> attempt.
/// </summary>
/// <remarks>
/// The adapter awaits every call to <see cref="OnEventAsync"/> before
/// continuing, so an observer that performs bounded, synchronous, or
/// cheaply asynchronous work provides natural backpressure. An observer
/// must not itself request cancellation of the run by throwing; it
/// observes and forwards events, and the caller's own
/// <see cref="CancellationToken"/> is the mechanism for stopping an
/// attempt early.
/// After attempt cancellation has been observed, the adapter delivers the
/// required terminal <see cref="ModelResponseCancelled"/> event with a
/// non-cancelable token so the returned outcome can remain identical to the
/// final delivered event. Observer work must therefore remain bounded.
/// </remarks>
public interface IModelResponseObserver
{
    /// <summary>Delivers the next event in one attempt's ordered event sequence.</summary>
    /// <param name="responseEvent">The event to deliver.</param>
    /// <param name="cancellationToken">
    /// A token used to cancel non-terminal delivery. The cancellation terminal
    /// is delivered with a non-cancelable token after attempt cancellation.
    /// </param>
    public ValueTask OnEventAsync(
        ModelResponseEvent responseEvent,
        CancellationToken cancellationToken = default);
}
