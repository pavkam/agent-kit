// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

/// <summary>Composes one session-backed <see cref="IInputQueue"/> over a real session store and two accepted lanes.</summary>
/// <remarks>
/// <para>
/// The fixture creates the queue through the public input/output registration, backed by whatever session store the
/// concrete fixture selected. It does not expose store internals. Callers admit and promote only through
/// <see cref="GetQueueAsync"/> and the request builders, which address lanes the fixture has already accepted.
/// </para>
/// <para>
/// <see cref="MaximumPendingInputsPerLane"/> is the ceiling configured on that queue. The shared suite uses it to
/// force a typed capacity failure without probing an unbounded default.
/// </para>
/// </remarks>
public interface IInputQueueConformanceFixture: IAsyncDisposable
{
    /// <summary>Gets the pending-input ceiling configured on the composed queue.</summary>
    /// <value>A positive per-queue bound. A new admission past this occupancy returns <see cref="QueueCapacityExceeded"/>.</value>
    public int MaximumPendingInputsPerLane { get; }

    /// <summary>Resolves the composed queue after the fixture has accepted both lanes.</summary>
    /// <param name="cancellationToken">Cancels setup before the queue is resolved.</param>
    /// <returns>The public <see cref="IInputQueue"/> selected by the fixture's dependency injection composition.</returns>
    public ValueTask<IInputQueue> GetQueueAsync(CancellationToken cancellationToken = default);

    /// <summary>Builds an admission request for one already-accepted lane and the supplied caller payload.</summary>
    /// <param name="input">The immutable caller input to admit. Replay equality uses this value's identity and content.</param>
    /// <param name="lane">The accepted lane that must own the admission.</param>
    /// <param name="cancellationToken">Cancels setup before the request is built.</param>
    /// <returns>A before-run admission request whose address, identity, and authorization match the selected lane.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="input"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lane"/> is undefined.</exception>
    public ValueTask<InputAdmissionRequest> CreateAdmissionRequestAsync(
        AgentInput input,
        InputQueueConformanceLane lane,
        CancellationToken cancellationToken = default);

    /// <summary>Builds a promotion request for one already-accepted lane using that lane's current operation revision.</summary>
    /// <param name="cutoffSequence">The inclusive admission cutoff. Later admissions on the lane must stay pending.</param>
    /// <param name="maximumPromotions">The positive selection bound passed through to the queue.</param>
    /// <param name="lane">The accepted lane whose pending input may be selected.</param>
    /// <param name="cancellationToken">Cancels setup or the revision reload before the request is built.</param>
    /// <returns>A promotion request bound to the lane's installed run, live branch cursor, and current state revision.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lane"/> is undefined or <paramref name="maximumPromotions"/> is not positive.</exception>
    public ValueTask<InputPromotionRequest> CreatePromotionRequestAsync(
        SessionSequence cutoffSequence,
        int maximumPromotions,
        InputQueueConformanceLane lane,
        CancellationToken cancellationToken = default);
}
