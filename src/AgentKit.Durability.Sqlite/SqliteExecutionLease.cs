// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Sqlite;

/// <summary>The SQLite manager's caller-owned handle to one granted ownership generation.</summary>
/// <remarks>
/// The handle holds no connection and caches no authority. Every renewal and release is decided by the database row,
/// which is the only authoritative record of the current generation, so a handle whose generation was superseded by
/// another process learns that from the store rather than from local state.
/// </remarks>
internal sealed class SqliteExecutionLease: IExecutionLease
{
    private readonly SqliteDurableLeaseManager _manager;
    private int _disposed;

    /// <summary>Initializes a caller-owned handle to a generation already recorded by the owning manager.</summary>
    /// <param name="manager">The manager that granted this generation and alone may renew or release it.</param>
    /// <param name="leaseId">The stable identity of this lease.</param>
    /// <param name="ownerWorkerId">The worker granted this generation.</param>
    /// <param name="address">The durable operation this generation owns.</param>
    /// <param name="fencingToken">The atomically allocated ownership generation.</param>
    /// <param name="duration">The requested lease length reused unchanged by every renewal.</param>
    /// <param name="expiresAt">The instant this generation expires unless renewed.</param>
    internal SqliteExecutionLease(
        SqliteDurableLeaseManager manager,
        ExecutionLeaseId leaseId,
        WorkerId ownerWorkerId,
        DurableOperationAddress address,
        FencingToken fencingToken,
        TimeSpan duration,
        DateTimeOffset expiresAt)
    {
        Debug.Assert(manager is not null, "The granting manager is required.");
        _manager = manager;
        LeaseId = leaseId;
        OwnerWorkerId = ownerWorkerId;
        Address = address;
        FencingToken = fencingToken;
        Duration = duration;
        ExpiresAt = expiresAt;
    }

    /// <inheritdoc/>
    public ExecutionLeaseId LeaseId { get; }

    /// <inheritdoc/>
    public WorkerId OwnerWorkerId { get; }

    /// <inheritdoc/>
    public DurableOperationAddress Address { get; }

    /// <inheritdoc/>
    public FencingToken FencingToken { get; }

    /// <summary>Gets the requested lease length reused unchanged by every renewal of this generation.</summary>
    /// <value>The positive duration the acquiring worker asked for.</value>
    internal TimeSpan Duration { get; }

    /// <inheritdoc/>
    public DateTimeOffset ExpiresAt { get; internal set; }

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">This lease has already been disposed.</exception>
    public ValueTask<LeaseRenewalResult> RenewAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        return _manager.RenewAsync(this, cancellationToken);
    }

    /// <inheritdoc/>
    /// <returns>A synchronously completed disposal; repeated disposal is harmless.</returns>
    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _manager.Release(this);
        }

        return ValueTask.CompletedTask;
    }
}
