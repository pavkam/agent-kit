// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Owns optimistic, idempotent, durable goal state behind an exact security boundary.</summary>
/// <remarks>
/// <para>
/// A store persists goals, attempts, and transitions; it does not own lifecycle policy, authorization decisions, or
/// events. Every operation except <see cref="ReadIntentsAsync"/> consumes a single-use <see cref="SecurityGrant"/> that
/// binds that exact operation before any state is read or written, and refuses a request whose authorized agent or
/// session does not own the addressed goal.
/// </para>
/// <para>
/// Implementations are thread-safe. Different goals progress concurrently; one goal's transitions are serialized by its
/// version token. Claims in <see cref="Descriptor"/> state the guarantees the adapter actually provides.
/// </para>
/// </remarks>
public interface IGoalStore
{
    /// <summary>Gets the store's identity and capability claims.</summary>
    /// <value>An immutable descriptor that does not change after construction.</value>
    public GoalStoreDescriptor Descriptor { get; }

    /// <summary>Durably creates one goal, or returns the original record for an equivalent replay.</summary>
    /// <param name="request">The exact authorized request.</param>
    /// <param name="cancellationToken">Cancels before the write commits.</param>
    /// <returns>The durable aggregate or a typed refusal; grant denial happens before any write.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before the write commits.</exception>
    public ValueTask<GoalCreateResult> CreateAsync(GoalCreateRequest request, CancellationToken cancellationToken = default);

    /// <summary>Loads one goal's complete durable state.</summary>
    /// <param name="request">The exact authorized request.</param>
    /// <param name="cancellationToken">Cancels before the read completes.</param>
    /// <returns>The aggregate or a typed refusal.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public ValueTask<GoalLoadResult> LoadAsync(GoalLoadRequest request, CancellationToken cancellationToken = default);

    /// <summary>Applies one transition and its attempt mutation atomically, or returns the original result for an equivalent replay.</summary>
    /// <param name="request">The exact authorized request.</param>
    /// <param name="cancellationToken">Cancels before the write commits.</param>
    /// <returns>The aggregate after the transition or a typed refusal.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before the write commits.</exception>
    public ValueTask<GoalTransitionResult> TransitionAsync(GoalTransitionRequest request, CancellationToken cancellationToken = default);

    /// <summary>Reads one page of a goal's children in recorded child-ordinal order.</summary>
    /// <param name="request">The exact authorized request.</param>
    /// <param name="cancellationToken">Cancels before the read completes.</param>
    /// <returns>A page or a typed refusal. Paging is stable under concurrent child creation because ordinals are append-only.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public ValueTask<GoalPageResult> ReadChildrenAsync(GoalChildrenRequest request, CancellationToken cancellationToken = default);

    /// <summary>Reads one page of open delegated children across sessions for a host worker.</summary>
    /// <param name="request">The host-configured scan request.</param>
    /// <param name="cancellationToken">Cancels before the read completes.</param>
    /// <returns>A page, or a rejection with <see cref="GoalStoreFailureKind.Unavailable"/> when <see cref="GoalStoreDescriptor.SupportsIntentDiscovery"/> is false, or <see cref="GoalStoreFailureKind.Denied"/> for an unconfigured scanner.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public ValueTask<GoalPageResult> ReadIntentsAsync(GoalIntentScanRequest request, CancellationToken cancellationToken = default);
}
