// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// An acquired, exclusive, cross-process claim to perform durable work for
/// one operation, carrying the fencing token every durable write must
/// present.
/// </summary>
/// <remarks>
/// <para>
/// This is the distributed counterpart to <see cref="ISessionRunLease"/>. A
/// session run lease coordinates within one process; an execution lease is
/// the ownership claim that survives process boundaries and can be lost to
/// takeover while its former owner is still running.
/// </para>
/// <para>
/// The lease is owned by the caller that acquired it and must be disposed
/// exactly once, normally with <see langword="await using"/> spanning the
/// durable work. Disposal releases ownership so another worker can take over
/// without waiting for expiry.
/// </para>
/// <para>
/// Holding a lease is not the same as still holding it. Expiry is a fact
/// about wall time, so a caller that was descheduled may hold an object whose
/// authority has already passed to another worker. That is why authority is
/// re-checked at every durable write rather than trusted from this handle.
/// </para>
/// </remarks>
public interface IExecutionLease: IAsyncDisposable
{
    /// <summary>Gets the stable identity of this lease.</summary>
    public ExecutionLeaseId LeaseId { get; }

    /// <summary>Gets the worker that owns this lease.</summary>
    public WorkerId OwnerWorkerId { get; }

    /// <summary>Gets the operation this lease grants ownership of.</summary>
    public DurableOperationAddress Address { get; }

    /// <summary>
    /// Gets the ownership generation that every durable write made under this
    /// lease must present.
    /// </summary>
    /// <value>
    /// A monotonically increasing value allocated atomically by the lease
    /// manager. It does not change across renewals.
    /// </value>
    public FencingToken FencingToken { get; }

    /// <summary>
    /// Gets the instant this lease expires unless it is renewed.
    /// </summary>
    /// <value>
    /// An instant produced from the lease manager's injected
    /// <see cref="TimeProvider"/>. Reading it is a snapshot, not a guarantee
    /// that the lease is still held now.
    /// </value>
    public DateTimeOffset ExpiresAt { get; }

    /// <summary>
    /// Attempts to extend this lease's expiry without changing its ownership
    /// generation.
    /// </summary>
    /// <param name="cancellationToken">
    /// A token that cancels the renewal attempt. Cancelling renewal does not
    /// release the lease; it stops trying to keep it alive.
    /// </param>
    /// <returns>
    /// <see cref="LeaseRenewed"/> with the extended expiry, or
    /// <see cref="LeaseLost"/> when ownership has already passed to another
    /// worker.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was signalled.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The lease has already been disposed.
    /// </exception>
    public ValueTask<LeaseRenewalResult> RenewAsync(CancellationToken cancellationToken = default);
}
