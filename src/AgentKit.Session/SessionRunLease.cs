// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session;

/// <summary>The default exact, process-local ownership lease for one accepted session execution lane.</summary>
internal sealed class SessionRunLease: ISessionRunLease
{
    private readonly DefaultSessionRunCoordinator _owner;
    private readonly SessionExecutionCapability _session;
    private readonly SessionOperationContext _context;
    private int _disposed;

    /// <summary>Initializes one validated process-local lease.</summary>
    /// <param name="owner">The coordinator that owns release linearization.</param>
    /// <param name="session">The exact compiled capability this lease was acquired through, retained only for an explicit later <see cref="ReleaseAsync"/> call.</param>
    /// <param name="request">The exact accepted-state lease request.</param>
    /// <param name="leaseId">The non-default lease identity.</param>
    /// <exception cref="ArgumentNullException">A required reference is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="leaseId"/> is default.</exception>
    internal SessionRunLease(DefaultSessionRunCoordinator owner, SessionExecutionCapability session,
        SessionRunLeaseRequest request, SessionLeaseId leaseId)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfEqual(leaseId, default);
        _owner = owner;
        _session = session;
        _context = request.Context;
        LeaseId = leaseId;
        StateRevision = request.ExpectedStateRevision;
    }

    /// <inheritdoc/>
    public SessionLeaseId LeaseId { get; }
    /// <inheritdoc/>
    /// <value>Read through the retained lease context; never a second stored copy.</value>
    public TenantId TenantId => _context.Identity.TenantId;
    /// <inheritdoc/>
    /// <value>Read through the retained lease context; never a second stored copy.</value>
    public AgentId AgentId => _context.AgentId;
    /// <inheritdoc/>
    /// <value>Read through the retained lease context; never a second stored copy.</value>
    public SessionId SessionId => _context.SessionId;
    /// <inheritdoc/>
    /// <value>Read through the retained lease context; never a second stored copy.</value>
    public ExecutionLaneId ExecutionLaneId => _context.ExecutionLaneId!.Value;
    /// <inheritdoc/>
    /// <value>Read through the retained lease context's in-run correlation; never a second stored copy.</value>
    public OperationId OperationId => ((InRunOperationCorrelation) _context.Correlation).OperationId;
    /// <inheritdoc/>
    /// <value>Read through the retained lease context's in-run correlation; never a second stored copy.</value>
    public RunId RunId => ((InRunOperationCorrelation) _context.Correlation).RunId;
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

    /// <inheritdoc/>
    public async ValueTask ReleaseAsync(CancellationToken cancellationToken = default)
    {
        await _owner.ReleaseDurableStateAsync(_session, _context, StateRevision, LeaseId, cancellationToken)
            .ConfigureAwait(false);
        await DisposeAsync().ConfigureAwait(false);
    }
}
