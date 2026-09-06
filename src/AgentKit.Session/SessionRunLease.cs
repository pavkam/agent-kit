// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session;

/// <summary>
/// The default, process-local <see cref="ISessionRunLease"/>. Disposing it
/// exactly once releases the session's active-run slot.
/// </summary>
internal sealed class SessionRunLease: ISessionRunLease
{
    private readonly DefaultSessionRunCoordinator _owner;
    private int _disposed;

    /// <summary>Initializes a new instance of the <see cref="SessionRunLease"/> class.</summary>
    /// <param name="owner">The coordinator to notify when this lease is released.</param>
    /// <param name="leaseId">The stable identity of this lease.</param>
    /// <param name="agentId">The agent that owns the leased session.</param>
    /// <param name="sessionId">The session this lease grants exclusive access to.</param>
    /// <param name="runId">The run holding this lease.</param>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> is null.</exception>
    public SessionRunLease(
        DefaultSessionRunCoordinator owner,
        SessionLeaseId leaseId,
        AgentId agentId,
        SessionId sessionId,
        RunId runId)
    {
        ArgumentNullException.ThrowIfNull(owner);

        _owner = owner;
        LeaseId = leaseId;
        AgentId = agentId;
        SessionId = sessionId;
        RunId = runId;
    }

    /// <inheritdoc/>
    public SessionLeaseId LeaseId { get; }

    /// <inheritdoc/>
    public AgentId AgentId { get; }

    /// <inheritdoc/>
    public SessionId SessionId { get; }

    /// <inheritdoc/>
    public RunId RunId { get; }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _owner.Release(new SessionAddress(AgentId, SessionId));
        }

        return ValueTask.CompletedTask;
    }
}
