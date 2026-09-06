// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Owns process-local single-active-mutating-run behavior for sessions.
/// </summary>
/// <remarks>
/// The default implementation enforces this rule with a process-local lock
/// and makes no distributed-safety claim. A durable execution adapter may
/// replace or augment this coordinator with fenced distributed leases for
/// multi-process deployments.
/// </remarks>
public interface ISessionRunCoordinator
{
    /// <summary>
    /// Attempts to acquire exclusive mutating access to a session for one
    /// run.
    /// </summary>
    /// <param name="request">The lease request.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task producing the terminal outcome.</returns>
    public ValueTask<SessionRunLeaseResult> AcquireAsync(
        SessionRunLeaseRequest request,
        CancellationToken cancellationToken = default);
}
