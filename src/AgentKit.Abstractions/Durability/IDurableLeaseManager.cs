// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Allocates exclusive cross-process ownership of durable operations and the
/// monotonic fencing tokens that make takeover safe.
/// </summary>
/// <remarks>
/// <para>
/// Implementations are used concurrently by many workers and must be
/// thread-safe. They are normally registered as singletons keyed by
/// <see cref="DurableLeaseManagerKey"/>.
/// </para>
/// <para>
/// Fencing tokens MUST be allocated atomically by the backing store and MUST
/// increase monotonically across every acquisition for an operation. An
/// in-process counter is not an acceptable implementation, because two
/// processes cannot coordinate through one, which is the only situation where
/// fencing matters.
/// </para>
/// <para>
/// Expiry MUST be computed and checked only through the injected
/// <see cref="TimeProvider"/>, never an ambient clock or a wall-clock delay,
/// so that takeover behavior is deterministic in tests.
/// </para>
/// <para>
/// Two workers only exclude each other when they contend on the same lease
/// manager. Composing an agent with two different lease managers for the same
/// operations does not produce mutual exclusion, and validation rejects it
/// rather than silently allowing split-brain ownership.
/// </para>
/// </remarks>
public interface IDurableLeaseManager
{
    /// <summary>
    /// Attempts to take exclusive ownership of one durable operation.
    /// </summary>
    /// <param name="request">
    /// The operation, requesting worker, and requested lease length.
    /// </param>
    /// <param name="cancellationToken">
    /// A token that cancels the acquisition attempt. Cancellation before a
    /// lease is granted acquires nothing.
    /// </param>
    /// <returns>
    /// <see cref="ExecutionLeaseAcquired"/> carrying a caller-owned lease, or
    /// <see cref="ExecutionLeaseHeldByAnotherWorker"/> when an unexpired
    /// lease is already held.
    /// </returns>
    /// <remarks>
    /// Acquisition does not wait for a busy lease by default. Returning the
    /// current owner and its expiry lets the caller decide whether to wait,
    /// escalate, or do other work, rather than blocking inside the store.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="request"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was signalled.
    /// </exception>
    public ValueTask<ExecutionLeaseResult> AcquireAsync(
        ExecutionLeaseRequest request,
        CancellationToken cancellationToken = default);
}
