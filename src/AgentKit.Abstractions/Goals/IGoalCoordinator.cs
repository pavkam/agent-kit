// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Owns goal lifecycle validation, authorization, and event publication over a selected store.</summary>
/// <remarks>
/// The coordinator never owns persistence mechanics: it selects the store named by the captured profile, asks the security
/// authority the command's captured authorization names for a single-use grant bound to the exact store operation, forwards
/// to the store, and publishes one immutable event per committed change. Commands carry captured authorization rather than
/// grants so that no caller has to reproduce the fingerprint of a request it does not build. Implementations are
/// thread-safe and may be singletons.
/// </remarks>
public interface IGoalCoordinator
{
    /// <summary>Authorizes and durably creates one goal.</summary>
    /// <param name="request">The command carrying captured authorization.</param>
    /// <param name="cancellationToken">Cancels before the write commits.</param>
    /// <returns>The durable aggregate or a typed refusal.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before the write commits.</exception>
    public ValueTask<GoalCreateResult> CreateAsync(GoalCreateCommand request, CancellationToken cancellationToken = default);

    /// <summary>Starts a new attempt by atomically moving a ready goal to active.</summary>
    /// <param name="request">The command carrying captured authorization.</param>
    /// <param name="cancellationToken">Cancels before the write commits.</param>
    /// <returns>The started attempt or a typed refusal.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before the write commits.</exception>
    public ValueTask<GoalAttemptResult> StartAttemptAsync(GoalAttemptRequest request, CancellationToken cancellationToken = default);

    /// <summary>Authorizes and applies one goal transition.</summary>
    /// <param name="request">The command carrying captured authorization.</param>
    /// <param name="cancellationToken">Cancels before the write commits.</param>
    /// <returns>The aggregate after the transition or a typed refusal.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before the write commits.</exception>
    public ValueTask<GoalTransitionResult> TransitionAsync(GoalTransitionCommand request, CancellationToken cancellationToken = default);

    /// <summary>Authorizes and loads one goal's complete durable state through the selected store.</summary>
    /// <param name="request">The command carrying captured authorization.</param>
    /// <param name="cancellationToken">Cancels before the read completes.</param>
    /// <returns>The aggregate or a typed refusal.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public ValueTask<GoalLoadResult> LoadAsync(GoalLoadCommand request, CancellationToken cancellationToken = default);

    /// <summary>Authorizes and reads one page of a goal's children through the selected store.</summary>
    /// <param name="request">The command carrying captured authorization.</param>
    /// <param name="cancellationToken">Cancels before the read completes.</param>
    /// <returns>A page in child-ordinal order or a typed refusal.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public ValueTask<GoalPageResult> ReadChildrenAsync(GoalChildrenCommand request, CancellationToken cancellationToken = default);
}
