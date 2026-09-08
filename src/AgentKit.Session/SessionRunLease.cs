// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session;

/// <summary>The default exact, process-local ownership lease for one accepted session execution lane.</summary>
internal sealed class SessionRunLease: ISessionRunLease
{
    private readonly DefaultSessionRunCoordinator _owner;
    private int _disposed;

    /// <summary>Initializes one validated process-local lease.</summary>
    /// <param name="owner">The coordinator that owns release linearization.</param>
    /// <param name="request">The exact accepted-state lease request.</param>
    /// <param name="leaseId">The non-default lease identity.</param>
    /// <exception cref="ArgumentNullException">A required reference is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="leaseId"/> is default.</exception>
    internal SessionRunLease(DefaultSessionRunCoordinator owner, SessionRunLeaseRequest request, SessionLeaseId leaseId)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfEqual(leaseId, default);
        _owner = owner;
        TenantId = request.Context.Identity.TenantId;
        LeaseId = leaseId;
        AgentId = request.AgentId;
        SessionId = request.SessionId;
        ExecutionLaneId = request.ExecutionLaneId;
        OperationId = request.OperationId;
        RunId = request.RunId;
        StateRevision = request.ExpectedStateRevision;
    }

    /// <inheritdoc/>
    public SessionLeaseId LeaseId { get; }
    /// <inheritdoc/>
    public TenantId TenantId { get; }
    /// <inheritdoc/>
    public AgentId AgentId { get; }
    /// <inheritdoc/>
    public SessionId SessionId { get; }
    /// <inheritdoc/>
    public ExecutionLaneId ExecutionLaneId { get; }
    /// <inheritdoc/>
    public OperationId OperationId { get; }
    /// <inheritdoc/>
    public RunId RunId { get; }
    /// <inheritdoc/>
    public OperationStateRevision StateRevision { get; }
    /// <inheritdoc/>
    public FencingToken? Fence => null;

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _owner.Release(TenantId, new SessionAddress(AgentId, SessionId), ExecutionLaneId, LeaseId);
        }
        return ValueTask.CompletedTask;
    }
}
